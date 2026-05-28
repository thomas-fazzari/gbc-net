using GbcNet.Core;

namespace GbcNet.Tests.RomTesting.Utils.ResultObservers;

internal interface IRomResultObserver
{
    RomTestObservation? Observe(GameBoy gameBoy);

    RomTestObservation Snapshot { get; }
}
