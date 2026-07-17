using NUnit.Framework;
using UnityEngine.UIElements;

namespace ImToolkit.Tests
{
    /// <summary>
    /// Structural tests for the immediate-mode reconciler, run in edit mode against a detached
    /// element tree (no panel, no events — interaction paths are covered by the sample scene).
    /// </summary>
    public class ImReconcilerTests
    {
        VisualElement m_Layer;
        int m_Frame;

        [SetUp]
        public void SetUp()
        {
            m_Layer = new VisualElement();
            ImContext.TestSetup(m_Layer);
            m_Frame = 1;
            ImContext.FrameSource = () => m_Frame;
        }

        [TearDown]
        public void TearDown() => ImContext.ResetStatics();

        void EndFrame()
        {
            ImContext.EndFrame();
            m_Frame++;
        }

        VisualElement MainPanel => ImContext.TestMainContainer.Element;

        [Test]
        public void Elements_AreReused_AcrossFrames()
        {
            IM.Label("hello");
            EndFrame();
            var first = MainPanel[0];

            IM.Label("world");
            EndFrame();

            Assert.AreEqual(1, MainPanel.childCount);
            Assert.AreSame(first, MainPanel[0]);
            Assert.AreEqual("world", ((Label)MainPanel[0]).text);
        }

        [Test]
        public void RemovedWidgets_ArePruned()
        {
            IM.Label("a");
            IM.Button("b");
            IM.Label("c");
            EndFrame();
            Assert.AreEqual(3, MainPanel.childCount);

            IM.Label("a");
            EndFrame();
            Assert.AreEqual(1, MainPanel.childCount);
            Assert.IsInstanceOf<Label>(MainPanel[0]);
        }

        [Test]
        public void ScanAhead_PreservesKeyedWidget_WhenEarlierWidgetsDisappear()
        {
            IM.Label("a");
            IM.Foldout("Stats");
            EndFrame();
            var foldout = MainPanel[1];

            IM.Foldout("Stats");
            EndFrame();

            Assert.AreEqual(1, MainPanel.childCount);
            Assert.AreSame(foldout, MainPanel[0]);
        }

        [Test]
        public void KindMismatch_ReplacesElement()
        {
            IM.Label("x");
            EndFrame();

            IM.Header("x");
            EndFrame();

            Assert.AreEqual(1, MainPanel.childCount);
            Assert.IsTrue(MainPanel[0].ClassListContains(ImStyles.Header));
        }

        [Test]
        public void CallerValueChanges_ArePushedIntoField()
        {
            IM.Slider("speed", 5f, 0f, 10f);
            EndFrame();
            var slider = (Slider)MainPanel[0];
            Assert.AreEqual(5f, slider.value);

            IM.Slider("speed", 7f, 0f, 10f);
            EndFrame();
            Assert.AreSame(slider, MainPanel[0]);
            Assert.AreEqual(7f, slider.value);
        }

        [Test]
        public void UserEdits_WinOverStaleCallerValue()
        {
            float value = IM.Slider("speed", 5f, 0f, 10f);
            EndFrame();
            var slider = (Slider)MainPanel[0];

            // Simulate a user edit between frames. Detached fields don't dispatch ChangeEvents
            // (no panel), so drive the sync state the callback would have written.
            slider.SetValueWithoutNotify(9f);
            var state = (ImValueState<float>)ImContext.TestMainContainer.Nodes[0].State;
            state.UserChanged = true;
            state.UserValue = 9f;

            value = IM.Slider("speed", value, 0f, 10f);
            EndFrame();
            Assert.AreEqual(9f, value);
            Assert.AreEqual(9f, slider.value);
        }

        [Test]
        public void Row_PrunesItsOwnChildren()
        {
            using (IM.Row())
            {
                IM.Label("a");
                IM.Label("b");
            }
            EndFrame();
            var row = MainPanel[0];
            Assert.AreEqual(2, row.childCount);

            using (IM.Row())
            {
                IM.Label("a");
            }
            EndFrame();
            Assert.AreSame(row, MainPanel[0]);
            Assert.AreEqual(1, row.childCount);
        }

        [Test]
        public void Window_HiddenWhenNotDrawn_AndRestoredOnRedraw()
        {
            using (IM.Window("Debug"))
                IM.Label("hi");
            EndFrame();

            Assert.AreEqual(1, ImContext.Windows.Count);
            var record = ImContext.Windows["Debug"];
            Assert.AreNotEqual((StyleEnum<DisplayStyle>)DisplayStyle.None, record.Root.style.display);

            EndFrame(); // nothing drawn this frame
            Assert.AreEqual((StyleEnum<DisplayStyle>)DisplayStyle.None, record.Root.style.display);

            using (IM.Window("Debug"))
                IM.Label("hi again");
            EndFrame();
            Assert.AreEqual((StyleEnum<DisplayStyle>)DisplayStyle.Flex, record.Root.style.display);
        }

        [Test]
        public void SameWindow_TwiceInOneFrame_AppendsInsteadOfPruning()
        {
            using (IM.Window("Debug"))
                IM.Label("from script A");
            using (IM.Window("Debug"))
                IM.Label("from script B");
            EndFrame();

            var content = ImContext.Windows["Debug"].Content.Element;
            Assert.AreEqual(2, content.childCount);
        }

        [Test]
        public void UnbalancedScope_RecoversNextFrame()
        {
            IM.Row(); // never disposed (simulates an exception inside the scope)
            IM.Label("inside");
            EndFrame();

            IM.Label("top-level");
            EndFrame();

            Assert.IsInstanceOf<Label>(MainPanel[0]);
            Assert.AreEqual("top-level", ((Label)MainPanel[0]).text);
        }
    }
}
