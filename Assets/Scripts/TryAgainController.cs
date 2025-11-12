using UnityEngine;

public class TryAgainClickTarget : MonoBehaviour
{
    [Tooltip("Kamera tujuan (ChessCam/Main Camera papan).")]
    public Camera targetCamera;

    [Tooltip("Matikan kamera lain saat pindah.")]
    public bool disableOtherCameras = true;

    [Tooltip("Set kamera tujuan sebagai MainCamera.")]
    public bool retagAsMain = true;

    void OnMouseDown() // klik pada collider (2D/3D)
    {
        if (!targetCamera)
        {
            // fallback cari nama umum
            targetCamera = FindCameraByName("ChessCam") ?? FindCameraByName("Main Camera");
            if (!targetCamera) { Debug.LogWarning("TryAgainClickTarget: targetCamera belum di-assign."); return; }
        }

        if (disableOtherCameras)
        {
            foreach (var cam in Camera.allCameras)
                cam.enabled = (cam == targetCamera);
        }
        targetCamera.enabled = true;

        if (retagAsMain)
        {
            // untag semua lalu tag target sebagai MainCamera
            foreach (var cam in Camera.allCameras) cam.tag = "Untagged";
            targetCamera.tag = "MainCamera";
        }

        Debug.Log("[TryAgain] Switched to: " + targetCamera.name);
    }

    Camera FindCameraByName(string name)
    {
        foreach (var cam in Camera.allCameras)
            if (cam.name == name) return cam;
        return null;
    }
}