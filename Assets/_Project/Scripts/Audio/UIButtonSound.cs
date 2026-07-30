using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour
{
    private Button button;
    private bool listenerRegistered;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError(
                gameObject.name
                + " 缺少 Button 组件，"
                + "UIButtonSound 无法工作。",
                this
            );
        }
    }

    private void OnEnable()
    {
        RegisterListener();
    }

    private void OnDisable()
    {
        RemoveListener();
    }

    private void OnDestroy()
    {
        RemoveListener();
    }

    private void RegisterListener()
    {
        if (listenerRegistered)
        {
            return;
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button == null)
        {
            return;
        }

        button.onClick.AddListener(PlayClickSound);
        listenerRegistered = true;
    }

    private void RemoveListener()
    {
        if (!listenerRegistered)
        {
            return;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(
                PlayClickSound
            );
        }

        listenerRegistered = false;
    }

    private void PlayClickSound()
    {
        if (button == null || !button.interactable)
        {
            return;
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.PlayButtonClick();
    }
}