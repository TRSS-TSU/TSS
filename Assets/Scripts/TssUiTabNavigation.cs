using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class TssUiTabNavigation : MonoBehaviour
{
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.tabKey.wasPressedThisFrame)
            return;

        var eventSystem = EventSystem.current;
        if (!eventSystem)
            return;

        var selected = eventSystem.currentSelectedGameObject;
        var canvas = selected ? selected.GetComponentInParent<Canvas>() : null;
        if (!canvas)
            return;

        var inputs = new List<InputField>();
        foreach (var input in canvas.GetComponentsInChildren<InputField>(false))
        {
            if (input.isActiveAndEnabled && input.interactable)
                inputs.Add(input);
        }

        if (inputs.Count == 0)
            return;

        var currentIndex = selected ? inputs.IndexOf(selected.GetComponent<InputField>()) : -1;
        var step = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? -1 : 1;
        var nextIndex = (currentIndex + step + inputs.Count) % inputs.Count;
        eventSystem.SetSelectedGameObject(inputs[nextIndex].gameObject);
        inputs[nextIndex].ActivateInputField();
    }
}
