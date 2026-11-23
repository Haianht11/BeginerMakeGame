using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class DungeonCameraFit : MonoBehaviour
{
    [SerializeField] private SpriteRenderer backdrop;

    private Camera view;

    private void Awake()
    {
        view = GetComponent<Camera>();
        FitRoom();
    }

    private void LateUpdate()
    {
        FitRoom();
    }

    private void FitRoom()
    {
        Vector3 halfRoom = backdrop.bounds.extents;
        view.orthographicSize = Mathf.Max(halfRoom.y, halfRoom.x / view.aspect);
    }
}
