#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JU.GameData.Demo.Tests
{
    public class GameDataDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator ButtonsDriveApprovalsShelterDayEndAndRestart()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/GameData/Demo/Scenes/GameDataDemo.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var controller = Object.FindObjectOfType<GameDataDemoController>();
            Assert.That(controller,Is.Not.Null);
            Assert.That(controller.CurrentState,Is.Not.Null);
            Assert.That(controller.CurrentState.day,Is.EqualTo(1));
            Assert.That(controller.CurrentState.npcs.Count,Is.EqualTo(8));
            Assert.That(controller.documentText.text,Does.Contain("NPC-"));
            Assert.That(controller.profileText.text,Does.Contain("羁绊溯源"));
            Assert.That(controller.debugText.text,Does.Contain("预计放行影响"));
            Assert.That(controller.documentText.font.HasCharacter('通'),Is.True,"字体应能显示中文。");
            string rejectedId=controller.SelectedNpcId;
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, controller.rejectButton.transform.position),
                button = PointerEventData.InputButton.Left
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count,Is.GreaterThan(0),"按钮应能接收鼠标射线。");
            Assert.That(hits[0].gameObject,Is.EqualTo(controller.rejectButton.gameObject));
            ExecuteEvents.Execute(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.That(controller.CurrentState.npcs.Single(n=>n.id==rejectedId).status,Is.EqualTo(NpcStatus.Sheltered));
            Assert.That(controller.SelectedNpcId,Is.Not.EqualTo(rejectedId));
            controller.utopiaButton.onClick.Invoke();
            controller.antiUtopiaButton.onClick.Invoke();
            Assert.That(controller.CurrentState.todayApprovals.Count,Is.EqualTo(2));
            controller.npcDropdown.value=controller.npcDropdown.options.FindIndex(o=>o.text.Contains(rejectedId));
            Assert.That(controller.SelectedNpcId,Is.EqualTo(rejectedId));
            Assert.That(controller.rejectButton.interactable,Is.False);
            controller.closeDayButton.onClick.Invoke();
            Assert.That(controller.CurrentState.day,Is.EqualTo(2));
            Assert.That(controller.CurrentState.npcs.Any(n=>n.id==rejectedId),Is.True);
            Assert.That(controller.feedbackText.text,Does.Contain("第 1 天结算"));
            controller.StartNewSession();
            for(int i=0;i<8;i++) controller.utopiaButton.onClick.Invoke();
            Assert.That(controller.SelectedNpcId,Is.Null);
            Assert.That(controller.utopiaButton.interactable,Is.False);
            Assert.That(controller.documentText.text,Does.Contain("没有待审批"));
            Assert.That(controller.closeDayButton.interactable,Is.True);
            controller.StartNewSession();
            for(int day=0;day<20&&!controller.CurrentState.gameOver;day++)
            {
                foreach(var npc in controller.CurrentState.npcs.Where(n=>n.status==NpcStatus.Pending).ToList())
                {
                    float u=GameSession.CalculateImpact(npc.attributes,Destination.Utopia);
                    float a=GameSession.CalculateImpact(npc.attributes,Destination.AntiUtopia);
                    if(System.Math.Min(u,a)>=0) continue;
                    controller.npcDropdown.value=controller.npcDropdown.options.FindIndex(o=>o.text.Contains(npc.id));
                    (u<=a?controller.utopiaButton:controller.antiUtopiaButton).onClick.Invoke();
                }
                controller.closeDayButton.onClick.Invoke();
            }
            Assert.That(controller.CurrentState.gameOver,Is.True);
            Assert.That(controller.closeDayButtonLabel.text,Is.EqualTo("重新开局"));
            Assert.That(controller.utopiaButton.interactable,Is.False);
            controller.closeDayButton.onClick.Invoke();
            Assert.That(controller.CurrentState.gameOver,Is.False);
            Assert.That(controller.CurrentState.day,Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
