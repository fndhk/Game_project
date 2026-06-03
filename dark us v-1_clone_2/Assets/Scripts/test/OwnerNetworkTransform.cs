using Photon.Pun;
using UnityEngine;

public class OwnerNetworkTransform : MonoBehaviourPunCallbacks
{
    public bool IsLocallyControlled
    {
        get
        {
            PhotonView view = GetComponent<PhotonView>();
            return view == null || view.IsMine;
        }
    }
}
