using UnityEngine;
using UnityEngine.UI;

namespace ShoppingList.Client;

/// <summary>
/// IMGUI windows don't stop clicks reaching the game's UI and hideout camera, so an invisible
/// uGUI panel sits under the window to catch them.
/// </summary>
internal sealed class ClickBlocker
{
    private RectTransform? _rect;

    public void Sync(bool active, Rect screenRect)
    {
        if (_rect == null)
        {
            if (!active)
            {
                return;
            }

            Create();
        }

        _rect!.gameObject.SetActive(active);
        if (active)
        {
            // IMGUI measures from the top, uGUI from the bottom
            _rect.anchoredPosition = new Vector2(screenRect.x, Screen.height - screenRect.yMax);
            _rect.sizeDelta = screenRect.size;
        }
    }

    private void Create()
    {
        var root = new GameObject("ShoppingListClickBlocker");
        Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        root.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        panel.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        _rect = (RectTransform)panel.transform;
        _rect.anchorMin = _rect.anchorMax = Vector2.zero;
        _rect.pivot = Vector2.zero;
    }
}
