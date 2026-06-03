using Photon.Pun;

public static class PhotonConnectionDefaults
{
    public const string GameVersion = "0.1.0";
    public const string FixedRegion = "asia";

    public static void Apply()
    {
        PhotonNetwork.GameVersion = GameVersion;

        if (PhotonNetwork.PhotonServerSettings != null &&
            PhotonNetwork.PhotonServerSettings.AppSettings != null)
        {
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = FixedRegion;
        }
    }
}
