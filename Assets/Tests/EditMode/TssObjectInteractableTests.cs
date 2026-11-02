using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TssObjectInteractableTests
{
    [Test]
    public void NearbyPlayerCountsAsInteractionRangeWithoutTrigger()
    {
        var interactableObject = new GameObject("Interactable");
        var player = new GameObject("Player");

        try
        {
            interactableObject.AddComponent<TssObjectInteractable>();
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 0f, 2f);

            Assert.IsTrue(IsPlayerInInteractionRange(interactableObject.GetComponent<TssObjectInteractable>()));

            player.transform.position = new Vector3(0f, 0f, 4f);
            Assert.IsFalse(IsPlayerInInteractionRange(interactableObject.GetComponent<TssObjectInteractable>()));
        }
        finally
        {
            Object.DestroyImmediate(interactableObject);
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void TerminationEntryDoesNotInstantiateGenericFirstPersonCanvas()
    {
        var interactableObject = new GameObject("TerminationEntry");
        var canvasPrefab = new GameObject("DesktopControlsPrefab");
        try
        {
            var interactable = interactableObject.AddComponent<TssObjectInteractable>();
            interactableObject.AddComponent<TssTerminationEntryAdapter>();
            SetPrivateField(interactable, "canvasPrefab", canvasPrefab);

            InvokePrivate(interactable, "ShowCanvas");

            Assert.IsNull(GetPrivateField(interactable, "_canvasInstance"));
        }
        finally
        {
            Object.DestroyImmediate(canvasPrefab);
            Object.DestroyImmediate(interactableObject);
        }
    }

    [Test]
    public void HandoffNotifiesReturnOnlyOnce()
    {
        var interactableObject = new GameObject("Interactable");
        try
        {
            var interactable = interactableObject.AddComponent<TssObjectInteractable>();
            var returnCount = 0;
            interactable.ReturnedToThirdPerson += () => returnCount++;
            SetPrivateField(interactable, "_isFirstPerson", true);

            InvokePrivate(interactable, "ReleaseFirstPersonForHandoff");
            interactableObject.SetActive(false);

            Assert.AreEqual(1, returnCount);
        }
        finally
        {
            Object.DestroyImmediate(interactableObject);
        }
    }

    [Test]
    public void DisableNotifiesReturnWhenFirstPersonIsActive()
    {
        var interactableObject = new GameObject("Interactable");
        try
        {
            var interactable = interactableObject.AddComponent<TssObjectInteractable>();
            var returnCount = 0;
            interactable.ReturnedToThirdPerson += () => returnCount++;
            SetPrivateField(interactable, "_isFirstPerson", true);

            interactableObject.SetActive(false);

            Assert.AreEqual(1, returnCount);
        }
        finally
        {
            Object.DestroyImmediate(interactableObject);
        }
    }

    private static bool IsPlayerInInteractionRange(TssObjectInteractable interactable)
    {
        var method = typeof(TssObjectInteractable).GetMethod("IsPlayerInInteractionRange", BindingFlags.Instance | BindingFlags.NonPublic);
        return (bool)method.Invoke(interactable, null);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        typeof(TssObjectInteractable).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        typeof(TssObjectInteractable).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        return typeof(TssObjectInteractable).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
