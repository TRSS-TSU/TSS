using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssCableScheduleTabPresenterTests
{
    [Test]
    public void ScenarioRowsUseMappedInterfaceOnNetworkDeviceOnly()
    {
        var root = new GameObject("Cable schedule test");
        var presenter = root.AddComponent<TssCableScheduleTabPresenter>();
        var content = new GameObject("Content").transform;
        content.SetParent(root.transform);
        var template = new GameObject("Template");
        template.transform.SetParent(content);
        for (var i = 0; i < 5; i++)
        {
            var cell = new GameObject("Cell" + i).AddComponent<Text>();
            cell.transform.SetParent(template.transform);
        }
        var notes = new GameObject("Notes");
        notes.transform.SetParent(content);

        SetPrivate(presenter, "tableContent", content);
        SetPrivate(presenter, "rowTemplate", template);

        var config = new NetworkScenarioConfig
        {
            requiredPorts = new[]
            {
                new NetworkRequiredPort { deviceRole = "AN-1", interfaceName = "Fa0/2", endpointId = "IT1" }
            }
        };
        var profile = TssNetworkRunProfile.Create(1, config);
        presenter.SetValues("TEST", profile, new[]
        {
            new ScenarioCableScheduleEntry { fromEndpoint = "IT1", fromPort = "NIC", toEndpoint = "AN-1", toPort = "Port 2", cableType = "Straight" },
            new ScenarioCableScheduleEntry { fromEndpoint = "AN-1", fromPort = "Port 1", toEndpoint = "AN-2", toPort = "Port 1", cableType = "Crossover" }
        });

        var generated = content.Find("GeneratedRow00");
        Assert.That(generated.GetComponentsInChildren<Text>(true)[3].text, Is.EqualTo("Fa0/2"));
        var unchanged = content.Find("GeneratedRow01");
        Assert.That(unchanged.GetComponentsInChildren<Text>(true)[1].text, Is.EqualTo("Port 1"));

        Object.DestroyImmediate(root);
    }

    private static void SetPrivate(object target, string name, object value)
    {
        typeof(TssCableScheduleTabPresenter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
