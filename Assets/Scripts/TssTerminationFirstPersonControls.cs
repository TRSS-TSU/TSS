using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssTerminationFirstPersonControls : MonoBehaviour
{
    [SerializeField] private Button frontViewButton;
    [SerializeField] private Button rearTerminationViewButton;

    private Action _frontView;
    private Action _rearTerminationView;

    private void Awake()
    {
        if (frontViewButton)
            frontViewButton.onClick.AddListener(() => _frontView?.Invoke());
        if (rearTerminationViewButton)
            rearTerminationViewButton.onClick.AddListener(() => _rearTerminationView?.Invoke());
    }

    public void Bind(Action frontView, Action rearTerminationView)
    {
        _frontView = frontView;
        _rearTerminationView = rearTerminationView;
    }
}
