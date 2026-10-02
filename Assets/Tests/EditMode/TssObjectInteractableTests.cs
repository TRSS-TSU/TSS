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

    private static bool IsPlayerInInteractionRange(TssObjectInteractable interactable)
    {
        var method = typeof(TssObjectInteractable).GetMethod("IsPlayerInInteractionRange", BindingFlags.Instance | BindingFlags.NonPublic);
        return (bool)method.Invoke(interactable, null);
    }
}
