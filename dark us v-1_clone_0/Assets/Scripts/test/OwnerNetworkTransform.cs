using Unity.Netcode.Components;
using UnityEngine;

// 클라이언트가 직접 자신의 위치 권한을 갖게 하는 프로의 방식
public class OwnerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false; // 서버 권한을 끄고 소유자(Client) 권한으로 설정
    }
}