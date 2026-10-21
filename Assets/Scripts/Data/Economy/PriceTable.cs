using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The price "spreadsheet". Single owner of every crop and seed price.</summary>
[CreateAssetMenu(fileName = "PriceTable", menuName = "MoFarmSand/Economy/Price Table")]
public class PriceTable : ScriptableObject
{
    [Serializable]
    public struct SeasonMult
    {
        public SeasonData season;
        public float mult;
    }

    [Serializable]
    public class PriceRow
    {
        public CropData crop;
        public SeedData seed;
        public int seedCost = 5;
        public int baseSell = 5;
        public List<SeasonMult> seasonMults = new List<SeasonMult>();
        [Tooltip("Seasons the merchant stocks the seed. Empty = all seasons.")]
        public List<SeasonData> availableSeasons = new List<SeasonData>();

        public float SeasonMultFor(SeasonData season)
        {
            for (int i = 0; i < seasonMults.Count; i++) if (seasonMults[i].season == season) return seasonMults[i].mult;
            return 1f;
        }

        public bool StockedIn(SeasonData season)
        {
            if (availableSeasons == null || availableSeasons.Count == 0) return true;
            return availableSeasons.Contains(season);
        }
    }

    public List<PriceRow> rows = new List<PriceRow>();

    public PriceRow RowForCrop(string cropId)
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].crop != null && rows[i].crop.Id == cropId) return rows[i];
        return null;
    }

    public PriceRow RowForSeed(string seedId)
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].seed != null && rows[i].seed.Id == seedId) return rows[i];
        return null;
    }

    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();
        HashSet<string> crops = new HashSet<string>();
        for (int i = 0; i < rows.Count; i++)
        {
            PriceRow r = rows[i];
            if (r.crop == null || r.seed == null) { errors.Add($"{name}: row {i} is missing a crop or seed"); continue; }
            if (!crops.Add(r.crop.Id)) errors.Add($"{name}: duplicate row for crop '{r.crop.Id}'");
            if (r.seed.grows != r.crop) errors.Add($"{name}: seed '{r.seed.Id}' does not grow crop '{r.crop.Id}'");
        }
        return errors.Count == 0;
    }
}
