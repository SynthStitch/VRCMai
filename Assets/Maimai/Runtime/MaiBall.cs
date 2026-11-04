// Hand ball pickup. Whoever grabs a ball becomes the player (owner) of the cabinet,
// so judging always runs on the machine holding the balls (no network latency on hits).
using UdonSharp;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MaiBall : UdonSharpBehaviour
{
    public MaiGame game;

    public override void OnPickup()
    {
        if (!Networking.IsOwner(game.gameObject)) game.TakeOver();
    }
}
