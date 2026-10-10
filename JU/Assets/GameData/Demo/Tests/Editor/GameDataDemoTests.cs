using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JU.GameData.Demo.Tests
{
    public class GameDataDemoTests
    {
        private const string Path = "Assets/GameData/Demo/Scenes/GameDataDemo.unity";
        [Test]
        public void SceneHasFivePanelsAndFourButtons()
        {
            var scene = EditorSceneManager.OpenScene(Path);
            var controller = Object.FindObjectOfType<GameDataDemoController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.configuration, Is.Not.Null);
            Assert.That(controller.transform.Cast<Transform>().Count(t => t.name.EndsWith("Panel")), Is.EqualTo(5));
            Assert.That(controller.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(4));
            Assert.That(Object.FindObjectOfType<EventSystem>(), Is.Not.Null);
            Assert.That(controller.npcDropdown.template, Is.Not.Null);
            Assert.That(controller.GetComponentsInChildren<ScrollRect>(true).Length, Is.EqualTo(4));
            foreach (var text in new[] {controller.dayLabel,controller.stateText,controller.queueCountText,controller.documentText,
                controller.profileText,controller.debugText,controller.feedbackText,controller.closeDayButtonLabel}) Assert.That(text,Is.Not.Null);
            Assert.That(scene.isDirty, Is.False);
        }

    }
}
