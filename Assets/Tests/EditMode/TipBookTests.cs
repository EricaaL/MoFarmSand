using NUnit.Framework;

public class TipBookTests
{
    [Test]
    public void FirstRaiseShowsImmediatelyAndLaterOnesQueueInOrder()
    {
        TipBook book = new TipBook();
        Assert.IsTrue(book.Raise("a"));
        Assert.IsTrue(book.Raise("b"));
        Assert.IsTrue(book.Raise("c"));
        Assert.AreEqual("a", book.Current);
        Assert.IsTrue(book.Dismiss());
        Assert.AreEqual("b", book.Current);
        Assert.IsTrue(book.Dismiss());
        Assert.AreEqual("c", book.Current);
        Assert.IsTrue(book.Dismiss());
        Assert.IsNull(book.Current);
        Assert.IsFalse(book.Dismiss());
    }

    [Test]
    public void ATipIsShownOnlyOnce()
    {
        TipBook book = new TipBook();
        book.Raise("a");
        Assert.IsFalse(book.Raise("a"), "already showing");
        book.Raise("b");
        Assert.IsFalse(book.Raise("b"), "already queued");
        book.Dismiss();
        book.Dismiss();
        Assert.IsFalse(book.Raise("a"), "already seen");
        Assert.IsNull(book.Current);
        Assert.IsTrue(book.HasSeen("a") && book.HasSeen("b"));
    }

    [Test]
    public void SeenIdsFromAPreviousSessionAreNotShownAgain()
    {
        TipBook book = new TipBook(new[] { TipIds.FirstDay });
        Assert.IsFalse(book.Raise(TipIds.FirstDay));
        Assert.IsTrue(book.Raise(TipIds.FirstRipe));
    }

    [Test]
    public void DisabledBookQueuesNothing()
    {
        TipBook book = new TipBook { Enabled = false };
        Assert.IsFalse(book.Raise("a"));
        Assert.IsNull(book.Current);
        book.Enabled = true;
        Assert.IsTrue(book.Raise("a"), "turning tips back on lets them show; nothing was marked seen");
    }

    [Test]
    public void ResetForgetsSeenAndClearsTheQueue()
    {
        TipBook book = new TipBook();
        book.Raise("a");
        book.Raise("b");
        book.Dismiss();
        book.Reset();
        Assert.IsNull(book.Current);
        Assert.AreEqual(0, book.Seen.Count);
        Assert.IsTrue(book.Raise("a"));
    }

    [Test]
    public void NullAndEmptyIdsAreIgnored()
    {
        TipBook book = new TipBook();
        Assert.IsFalse(book.Raise(null));
        Assert.IsFalse(book.Raise(string.Empty));
        Assert.IsNull(book.Current);
    }
}
