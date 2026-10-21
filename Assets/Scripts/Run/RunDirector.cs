using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum RunMode { GoldenSlice, Roguelike }

/// <summary>
/// The only place live scene components and <see cref="RunState"/> meet. Each map node is one farm day:
/// the scene reloads at every boundary, and this restores the saved run into the fresh scene.
/// In GoldenSlice mode it does nothing, so the original single-day trailer loop is untouched.
/// </summary>
public class RunDirector : MonoBehaviour, IDayOutcomeHandler
{
    [SerializeField] private RunMode runMode = RunMode.Roguelike;
    [SerializeField] private GameDatabase db;
    [Tooltip("Beta test scene: uses the hidden beta save slot, skips the title screen and lets you enter any map node.")]
    [SerializeField] private bool betaMode;

    [Header("Scene")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerWallet wallet;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private GameObject bossRoot;
    [SerializeField] private BossHunger bossHunger;
    [SerializeField] private FarmConditions conditions;
    [SerializeField] private DisasterDirector disaster;
    [SerializeField] private CozyBridge cozyBridge;
    [SerializeField] private GameObject merchantStall;
    [SerializeField] private DayClock clock;

    private enum View { None, Title, Map, Event, Bank, Shop, Summary, Items, Pause, Route }

    private View view;
    private IRunScreen screen;
    private View pauseReturnView;
    private IRunScreen pauseReturnScreen;
    private PauseScreen pauseScreen;
    private MapScreen routeScreen;
    private TitleScreen titleScreen;
    private MapScreen mapScreen;
    private EventScreen eventScreen;
    private BankScreen bankScreen;
    private ShopScreen shopScreen;
    private SummaryScreen summaryScreen;
    private ItemInfoScreen itemInfoScreen;
    private RunHud hud;
    private DayPlan plan;
    private bool dayRunning;
    private bool resolving;
    private bool leaving; // the scene is about to reload: stop drawing and ignore input

    /// <summary>True while a full-screen run screen is up. DebugHUD hides itself then.</summary>
    public static bool ModalOpen { get; private set; }

    public bool IsRoguelike => runMode == RunMode.Roguelike;
    public bool IsBeta => betaMode;
    public bool IsDayRunning => dayRunning;
    /// <summary>The merchant is at his booth every day. Merchant days additionally stock charms (see DayPlanner).</summary>
    public bool MerchantOpen => dayRunning && plan != null;
    public GameDatabase Db => db;
    public SaveFile File => RunSession.Current;
    public MetaProgress Meta => RunSession.Current != null ? RunSession.Current.meta : null;
    public RunState Run => RunSession.Current != null ? RunSession.Current.run : null;
    public DayPlan Plan => plan;
    public PlayerWallet Wallet => wallet;
    public PlayerInventory Inventory => inventory;
    public DisasterDirector Disaster => disaster;
    public DayClock Clock => clock;
    public FarmConditions Conditions => conditions;
    public SaveService Service => RunSession.Service;
    public int LastBankMessageCredited { get; set; }
    private const int BetaCoins = 999;
    /// <summary>Plots added by each extra farmland (one column of the field).</summary>
    public int PlotsPerFarmland { get; private set; } = 3;

    private void Awake()
    {
        ModalOpen = false;
        Time.timeScale = 1f; // a previous scene may have been left paused
        if (!IsRoguelike) return;
        titleScreen = new TitleScreen(this);
        mapScreen = new MapScreen(this);
        routeScreen = new MapScreen(this, readOnly: true);
        pauseScreen = new PauseScreen(this);
        eventScreen = new EventScreen(this);
        bankScreen = new BankScreen(this);
        shopScreen = new ShopScreen(this);
        summaryScreen = new SummaryScreen(this);
        itemInfoScreen = new ItemInfoScreen(this);
        hud = new RunHud(this);
    }

    private void Update()
    {
        if (!IsRoguelike || leaving) return;
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // Tab toggles the Items tab during a day (it pauses the world like any other screen).
        if (kb.tabKey.wasPressedThisFrame)
        {
            if (view == View.Items) CloseItems();
            else if (dayRunning && view == View.None) ShowItems();
        }

        // Esc opens the run menu (on a day or on the map) and backs out of its sub-screens.
        // The shop handles its own Esc, so it is deliberately not listed here.
        if (kb.escapeKey.wasPressedThisFrame)
        {
            switch (view)
            {
                case View.Items: CloseItems(); break;
                case View.Route: CloseRoute(); break;
                case View.Pause: ClosePause(); break;
                case View.None: if (dayRunning) ShowPause(); break;
                case View.Map: ShowPause(); break;
            }
        }
    }

    private void Start()
    {
        if (!IsRoguelike) return;
        if (gameManager != null) gameManager.OutcomeHandler = this;

        FarmTile[] tiles = FindObjectsByType<FarmTile>(FindObjectsSortMode.None);
        List<Vector3> basePositions = new List<Vector3>();
        foreach (FarmTile t in tiles)
        {
            t.SetFreePlanting(false);
            t.SetConditions(conditions);
            basePositions.Add(t.transform.position);
        }
        // How many plots one extra farmland adds (the length of a column), for the shop text.
        PlotsPerFarmland = Mathf.Max(1, FarmlandLogic.PlotsPerFarmland(basePositions));
        SetFrozen(true);

        if (betaMode) { StartBeta(); return; }
        // The beta slot is private to the beta scene: never carry it into the normal game.
        if (RunSession.ActiveSlot == SaveService.BetaSlot) RunSession.Unload();

        if (RunSession.HasActiveSlot && Run != null && File.hasRun) Route();
        else ShowTitle();
    }

    /// <summary>Beta scene: open the hidden slot and go straight to the free-roam map (or the day in progress).</summary>
    private void StartBeta()
    {
        if (RunSession.ActiveSlot != SaveService.BetaSlot) OpenSlot(SaveService.BetaSlot);
        if (!RunSession.HasActiveSlot) return;
        bool usable = File.hasRun && Run.freeRoam && Run.phase != RunPhase.Won && Run.phase != RunPhase.Lost;
        if (usable) Route(); else StartNewRun();
    }

    /// <summary>Beta: throws the beta save away and starts a fresh free-roam year.</summary>
    public void ResetBetaSlot()
    {
        if (!betaMode) return;
        leaving = true;
        Time.timeScale = 1f;
        if (clock != null) clock.Stop();
        if (disaster != null) disaster.End();
        Service.Delete(SaveService.BetaSlot);
        RunSession.Unload();
        ReloadScene();
    }

    /// <summary>Beta: drops today's day without finishing it and returns to the map.</summary>
    public void LeaveBetaDay()
    {
        if (!betaMode || Run == null) return;
        leaving = true;
        Time.timeScale = 1f;
        if (clock != null) clock.Stop();
        if (disaster != null) disaster.End();
        dayRunning = false;
        Run.pendingNodeId = -1;
        Run.hp = Run.maxHp;
        Run.phase = RunPhase.ChoosingNode;
        RunSession.Commit();
        ReloadScene();
    }

    /// <summary>Beta: changes the year number, which scales every boss (see BossScaling).</summary>
    public void SetBetaYear(int year)
    {
        if (!betaMode || Run == null) return;
        Run.year = Mathf.Max(1, year);
        RunSession.Commit();
    }

    private void OnDestroy()
    {
        ModalOpen = false;
        if (gameManager != null && gameManager.OutcomeHandler == (IDayOutcomeHandler)this) gameManager.OutcomeHandler = null;
    }

    // ---------------------------------------------------------------- routing

    private void Route()
    {
        RunState run = Run;
        wallet.Set(run.coins);
        switch (run.phase)
        {
            case RunPhase.ChoosingNode: ShowMap(); break;
            case RunPhase.InDay: StartDay(run.pendingNodeId); break;
            case RunPhase.Banking: ShowBank(); break;
            default: ShowSummary(); break;
        }
    }

    private void Show(View v, IRunScreen s)
    {
        view = v;
        screen = s;
        ModalOpen = s != null;
        SetFrozen(s != null || !dayRunning);
        if (playerInteractor != null) playerInteractor.enabled = s == null && dayRunning;

        // Any screen opened mid-day (menu, shop, items) freezes the world: no boss chasing while you read.
        Time.timeScale = s != null && dayRunning ? 0f : 1f;
    }

    private void SetFrozen(bool frozen)
    {
        if (playerController != null) playerController.CanMove = !frozen;
        if (playerCombat != null) playerCombat.CanAct = !frozen;
    }

    public void ShowTitle() { dayRunning = false; Show(View.Title, titleScreen); }
    public void ShowMap() { dayRunning = false; Show(View.Map, mapScreen); }
    public void ShowBank() { dayRunning = false; Show(View.Bank, bankScreen); }
    public void ShowSummary() { dayRunning = false; Show(View.Summary, summaryScreen); }

    public void ShowItems()
    {
        if (!dayRunning || plan == null) return;
        itemInfoScreen.OnOpen();
        Show(View.Items, itemInfoScreen);
    }

    public void CloseItems() { if (view == View.Items) Show(View.None, null); }

    // ---------------------------------------------------------------- run menu

    /// <summary>Opens the run menu from a day or from the map, remembering where to return.</summary>
    public void ShowPause()
    {
        if (view != View.None && view != View.Map) return;
        if (view == View.None && !dayRunning) return;
        pauseReturnView = view;
        pauseReturnScreen = screen;
        pauseScreen.OnOpen();
        Show(View.Pause, pauseScreen);
    }

    public void ClosePause()
    {
        if (view != View.Pause) return;
        Show(pauseReturnView, pauseReturnScreen);
    }

    /// <summary>Read-only route map for use inside a day.</summary>
    public void ShowRoute()
    {
        if (view != View.Pause) return;
        routeScreen.OnOpen();
        Show(View.Route, routeScreen);
    }

    public void CloseRoute() { if (view == View.Route) Show(View.Pause, pauseScreen); }

    public void ShowItemsFromPause()
    {
        if (view != View.Pause) return;
        // Closing the items tab returns to play; Resume would have done the same.
        pauseReturnView = View.None;
        pauseReturnScreen = null;
        itemInfoScreen.OnOpen();
        Show(View.Items, itemInfoScreen);
    }

    /// <summary>Gives up the year right now. Purse coins are lost; the upgrade shop comes next.</summary>
    public void AbandonRun()
    {
        if (Run == null) return;
        leaving = true;
        Time.timeScale = 1f;
        if (clock != null) clock.Stop();
        if (disaster != null) disaster.End();
        if (dayRunning && wallet != null) Run.coins = wallet.Coins; // so "coins lost" reflects the live purse
        dayRunning = false;

        RunLogic.FailRun(Run, Meta);
        RunSession.Commit();
        ReloadScene(); // a clean scene, then the end-of-year screen with the upgrade shop
    }

    /// <summary>Today's conditions for the item descriptions (grow times, prices, demand).</summary>
    public ItemInfoContext BuildItemContext(ItemData item)
    {
        return new ItemInfoContext
        {
            db = db,
            date = plan.date,
            weather = plan.weather,
            extraGrowth = conditions != null ? conditions.Growth.Product : 1f,
            sellShock = disaster != null ? disaster.SellPrice.Product : 1f,
            seedShock = disaster != null ? disaster.SeedPrice.Product : 1f,
            market = Run.market,
            owned = inventory != null ? inventory.CountOf(item) : 0,
            ownedQuality = inventory != null ? inventory.QualityOf(item) : 1f,
            lockedBy = CropAvailability.LockedBehind(item, Run.unlockedIds, db),
            bossScale = BossScaling.ForYear(Run.YearNumber(Meta), db.difficulty)
        };
    }

    public void OpenShop() { if (dayRunning) Show(View.Shop, shopScreen); }
    public void CloseShop() { if (view == View.Shop) Show(View.None, null); }

    // ---------------------------------------------------------------- slots and runs

    public void OpenSlot(int slot)
    {
        LoadResult r = Service.Load(slot);
        SaveFile file = r.status == LoadStatus.Ok ? r.file : new SaveFile();
        if (r.status == LoadStatus.TooNew)
        {
            Debug.LogWarning($"RunDirector: slot {slot} was saved by a newer version; not opening it.");
            return;
        }
        SaveService.Sanitize(file, db);
        RunSession.ActiveSlot = slot;
        RunSession.Current = file;
    }

    public void CloseSlot() { RunSession.Unload(); }

    public void DeleteSlot(int slot)
    {
        Service.Delete(slot);
        if (RunSession.ActiveSlot == slot) RunSession.Unload();
    }

    public void ContinueRun()
    {
        if (!RunSession.HasActiveSlot || !File.hasRun) return;
        Route();
    }

    public void StartNewRun()
    {
        if (!RunSession.HasActiveSlot) return;
        int seed = Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);
        File.run = RunLogic.NewRun(seed, db, File.meta);
        if (betaMode)
        {
            File.run.freeRoam = true;
            File.run.coins = BetaCoins;
        }
        File.hasRun = true;
        RunSession.Commit();
        Route();
    }

    public bool BuyStockroom()
    {
        if (!RunSession.HasActiveSlot || !StockroomLogic.TryBuy(Meta, db.stockroom)) return false;
        RunSession.Commit();
        return true;
    }

    public bool BuyVault()
    {
        if (!RunSession.HasActiveSlot || !VaultLogic.TryBuy(Meta, db.bank)) return false;
        RunSession.Commit();
        return true;
    }

    public bool BuyFarmland()
    {
        if (!RunSession.HasActiveSlot || !FarmlandLogic.TryBuy(Meta, db.farmland)) return false;
        RunSession.Commit();
        return true;
    }

    public bool BuyUnlock(UnlockData unlock)
    {
        if (!RunSession.HasActiveSlot || !BankLogic.TryBuyUnlock(Meta, unlock)) return false;
        RunSession.Commit();
        return true;
    }

    public void QuitToTitle()
    {
        leaving = true;
        Time.timeScale = 1f;
        RunSession.Unload();
        ReloadScene();
    }

    // ---------------------------------------------------------------- map and days

    public void ChooseNode(int nodeId)
    {
        if (!RunLogic.BeginNode(Run, nodeId)) return;
        RunSession.Commit();
        StartDay(nodeId);
    }

    private void StartDay(int nodeId)
    {
        plan = DayPlanner.Plan(Run, nodeId, db);
        if (plan.evt != null && Run.eventResolvedNodeId != nodeId)
        {
            Show(View.Event, eventScreen);
            return;
        }
        StartCoroutine(BeginDayRoutine());
    }

    public void ResolveEvent(int optionIndex)
    {
        if (plan == null || plan.evt == null || optionIndex < 0 || optionIndex >= plan.evt.options.Count) return;
        EventData.EventOption option = plan.evt.options[optionIndex];
        RunState run = Run;
        if (run.coins < option.coinCost) return;

        run.coins -= option.coinCost;
        if (option.effects != null)
            foreach (RunEffect e in option.effects) if (e != null) e.Apply(run, db);
        run.eventResolvedNodeId = plan.nodeId;
        RunSession.Commit();
        StartCoroutine(BeginDayRoutine());
    }

    private IEnumerator BeginDayRoutine()
    {
        // One frame lets COZY finish its own Start before we set date and weather.
        Show(View.None, null);
        SetFrozen(true);
        yield return null;

        RunState run = Run;
        if (plan.node != null && plan.node.healOnStart) run.hp = run.maxHp;
        if (betaMode)
        {
            // Every beta day starts fully stocked for its season, so any boss can be tried at once.
            run.hp = run.maxHp;
            run.coins = Mathf.Max(run.coins, BetaCoins);
            BetaKit.Fill(run, db, plan.date.season);
        }

        inventory.Restore(run.inventory, run.activeSlot, db.items);
        health.Restore(run.hp, run.maxHp);
        wallet.Set(run.coins);

        BuildFarmlands(run.farmlands);

        conditions.Growth.Clear();
        conditions.Growth.Set("day", plan.growthMultiplier);
        conditions.Configure(plan.date, plan.weather);
        conditions.Bonus = new HarvestBonus(db, run, SeededRng.Derive(run.runSeed, plan.nodeId, RngStreams.HarvestBonus));

        DisasterData data = plan.disaster.IsNone || db.disasters == null ? null : db.disasters.Get(plan.disaster.disasterId);
        disaster.Begin(plan.disaster, run.runSeed, plan.nodeId);
        cozyBridge.Apply(plan, data, db.cozyWeather);

        bool encounter = plan.encounter != null;
        gameManager.Configure(new DayRules { hasEncounter = encounter, survivableLoss = encounter && plan.node != null && plan.node.survivableLoss });
        if (bossRoot != null) bossRoot.SetActive(encounter);
        if (encounter && bossHunger != null)
        {
            bossHunger.Configure(plan.encounter);
            // Bosses grow stronger every year: more feeding needed, faster, eventually harder-hitting.
            bossHunger.ApplyScaling(BossScaling.ForYear(run.YearNumber(Meta), db.difficulty));
        }

        dayRunning = true;
        Show(View.None, null);

        // Limited time per day: daylight counts down, then night falls by itself.
        if (clock != null) clock.Begin(plan.dayLengthSeconds, plan.nightLengthSeconds);

        // Boss days skip the daytime: night falls at once and the fight starts. The clock was armed first,
        // so it switches straight to the night timer.
        if (plan.startsAtNight) gameManager.StartNight(instant: true);
    }

    /// <summary>Adds the extra farmland columns you've bought. The disaster director gathers plots afterwards, so they're included.</summary>
    private void BuildFarmlands(int extra)
    {
        if (extra <= 0) return;
        FarmTile[] tiles = FindObjectsByType<FarmTile>(FindObjectsSortMode.None);
        if (tiles.Length == 0) return;
        FarmTile template = tiles[0];

        // Keep new columns off the merchant's booth and the bell.
        List<Vector3> keepClear = new List<Vector3>();
        if (merchantStall != null) keepClear.Add(merchantStall.transform.position);
        BellTrigger bell = FindFirstObjectByType<BellTrigger>();
        if (bell != null) keepClear.Add(bell.transform.position);
        FarmlandSpawner.Spawn(extra, template, tiles, template.transform.parent, conditions, db.farmland != null ? db.farmland.maxColumnsPerSide : 4, keepClear);
    }

    // ---------------------------------------------------------------- day end

    public void OnDayResolved(GamePhase outcome)
    {
        if (resolving || !IsRoguelike) return;
        resolving = true;
        StartCoroutine(ResolveRoutine(outcome));
    }

    private IEnumerator ResolveRoutine(GamePhase phase)
    {
        dayRunning = false;
        if (clock != null) clock.Stop();
        RunState run = Run;

        // Snapshot the live scene back into the run.
        run.inventory = inventory.Snapshot();
        run.activeSlot = inventory.ActiveIndex;
        run.hp = health.Hp;
        run.maxHp = health.MaxHp;
        run.coins = wallet.Coins;
        // Whatever is left on the shelf carries to the next day. Merchant days refill it, so this is the refilled shelf minus purchases.
        run.shopStock = ShopStock.FromOffers(plan.offers);

        DayOutcome outcome = phase == GamePhase.Lost ? DayOutcome.Failed
            : phase == GamePhase.Won ? DayOutcome.BossDefeated
            : DayOutcome.Survived;
        RunLogic.CompleteNode(run, Meta, outcome, plan, db);
        RunSession.Commit();

        yield return new WaitForSecondsRealtime(db.run.dayEndDelay);
        ReloadScene();
    }

    private void ReloadScene() { SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }

    // ---------------------------------------------------------------- bank

    public DepositResult Deposit(int amount)
    {
        DepositResult r = BankLogic.Deposit(Run, Meta, amount, db.bank);
        if (r.spent > 0) { wallet.Set(Run.coins); RunSession.Commit(); }
        return r;
    }

    public WithdrawResult Withdraw(int amount)
    {
        WithdrawResult r = BankLogic.Withdraw(Run, Meta, amount, db.bank);
        if (r.taken > 0) { wallet.Set(Run.coins); RunSession.Commit(); }
        return r;
    }

    public void FinishBanking()
    {
        RunLogic.FinishBanking(Run, Meta);
        RunSession.Commit();
        if (Run.phase == RunPhase.Won) ShowSummary(); else ShowMap();
    }

    // ---------------------------------------------------------------- shop

    public int OfferPrice(ShopOffer offer)
    {
        if (offer.kind == OfferKind.Modifier) return offer.price;
        PriceTable.PriceRow row = db.prices.RowForSeed(offer.id);
        float shock = disaster != null ? disaster.SeedPrice.Product : 1f;
        return EconomyModel.SeedPrice(row, plan.date.season, db.economy, shock);
    }

    public bool Buy(int offerIndex)
    {
        if (plan == null || offerIndex < 0 || offerIndex >= plan.offers.Count) return false;
        ShopOffer offer = plan.offers[offerIndex];
        if (offer.stock <= 0) return false;
        int price = OfferPrice(offer);
        if (wallet.Coins < price) return false;

        if (offer.kind == OfferKind.Seed)
        {
            if (!db.items.TryGet(offer.id, out ItemData item) || !inventory.AddItem(item, 1)) return false;
        }
        else
        {
            if (!db.modifiers.TryGet(offer.id, out RunModifierData mod)) return false;
            Run.modifierIds.Add(mod.Id);
            if (mod.bonusMaxHp != 0) health.Restore(health.Hp + Mathf.Max(0, mod.bonusMaxHp), health.MaxHp + mod.bonusMaxHp);
        }

        wallet.TrySpend(price);
        offer.stock--;
        plan.offers[offerIndex] = offer;
        return true;
    }

    public int SellPriceOf(CropData crop)
    {
        PriceTable.PriceRow row = db.prices.RowForCrop(crop.Id);
        float shock = disaster != null ? disaster.SellPrice.Product : 1f;
        return EconomyModel.SellPrice(row, plan.date.season, Run.market.GetDemand(crop.Id), shock);
    }

    /// <summary>Sells up to <paramref name="count"/> crops one at a time so each sale lowers the next price.</summary>
    public int Sell(CropData crop, int count)
    {
        int earned = 0;
        for (int i = 0; i < count; i++)
        {
            if (!inventory.TryRemove(crop, 1)) break;
            int price = SellPriceOf(crop);
            wallet.Add(price);
            earned += price;
            EconomyModel.ApplySale(Run.market, crop.Id, 1, db.economy);
        }
        return earned;
    }

    // ---------------------------------------------------------------- drawing

    private void OnGUI()
    {
        if (!IsRoguelike || leaving) return;
        // Between unloading the slot and the scene reload there can be one more frame: don't draw run screens with no run.
        if (view != View.Title && Run == null) return;
        GUI.depth = -10;
        if (screen != null) screen.Draw();
        else if (dayRunning) hud.Draw();
    }
}
