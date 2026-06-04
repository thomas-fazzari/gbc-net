namespace GbcNet.Tests.RomTesting.Utils.ResultObservers;

internal interface IRomResultObserver
{
    RomTestObservation? Observe();

    RomTestObservation Snapshot { get; }
}
