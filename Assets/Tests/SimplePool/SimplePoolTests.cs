using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ModuleFactory.SimplePooling.Tests
{
    public sealed class SimplePoolTests
    {
        private SimplePool pool;
        private GameObject template;
        [SetUp]
        public void SetUp()
        {
            pool = new GameObject("Simple Pool Test").AddComponent<SimplePool>();
            template = new GameObject("Template"); template.SetActive(false); template.AddComponent<SimplePoolProbe>();
        }
        [TearDown]
        public void TearDown()
        { Time.timeScale = 1; if (pool != null) Object.DestroyImmediate(pool.gameObject); Object.DestroyImmediate(template); }
        [Test]
        public void SpawnResetsThenInitializesBeforeEnable()
        {
            var instance = pool.Spawn(template, Vector3.one, initialize: go => go.GetComponent<SimplePoolProbe>().Value = 42);
            Assert.IsNotNull(instance); Assert.IsTrue(instance.activeInHierarchy);
            Assert.AreEqual(Vector3.one, instance.transform.position);
            Assert.AreEqual(42, instance.GetComponent<SimplePoolProbe>().ValueOnEnable);
        }
        [Test]
        public void ReuseResetsStateAndDuplicateOrWrongPoolReturnIsRejected()
        {
            var a = pool.Spawn(template); a.GetComponent<SimplePoolProbe>().Value = 99;
            var other = new GameObject("Other Pool").AddComponent<SimplePool>();
            try
            {
                Assert.IsFalse(other.Despawn(a)); Assert.IsTrue(pool.Despawn(a)); Assert.IsFalse(pool.Despawn(a));
                Assert.AreSame(a, pool.Spawn(template)); Assert.AreEqual(0, a.GetComponent<SimplePoolProbe>().Value);
                Assert.AreEqual(2, a.GetComponent<SimplePoolProbe>().Rents);
            }
            finally { Object.DestroyImmediate(other.gameObject); }
        }
        [Test]
        public void PrewarmAndCapacityPreventUnboundedCreation()
        {
            Assert.IsTrue(pool.Configure(template, 2, 2)); Assert.AreEqual(2, pool.CountAll(template));
            Assert.IsNotNull(pool.Spawn(template)); Assert.IsNotNull(pool.Spawn(template));
            Assert.IsNull(pool.Spawn(template)); Assert.AreEqual(2, pool.CountActive(template));
            Assert.IsFalse(pool.Prewarm(template, 3)); Assert.IsFalse(pool.Configure(template, 1));
        }
        [Test]
        public void ReuseRestoresScaleAndRequestedParent()
        {
            template.transform.localScale = Vector3.one * 2;
            var a = pool.Spawn(template); a.transform.localScale = Vector3.one * 9; pool.Despawn(a);
            Assert.AreSame(a, pool.Spawn(template, Vector3.right, parent: pool.transform));
            Assert.AreEqual(Vector3.one * 2, a.transform.localScale);
        }
        [UnityTest]
        public IEnumerator OldTimerCannotReturnReusedObject()
        {
            var a = pool.Spawn(template); pool.DespawnAfter(a, 0.01f, true); pool.Despawn(a);
            var b = pool.Spawn(template); yield return new WaitForSecondsRealtime(0.08f);
            Assert.IsTrue(b.activeSelf); Assert.AreEqual(1, pool.CountActive(template));
        }
        [UnityTest]
        public IEnumerator TimerCanBeReplacedAndUnscaledTimerWorksDuringPause()
        {
            var a = pool.Spawn(template); pool.DespawnAfter(a, 20); pool.DespawnAfter(a, 0.02f, true);
            Time.timeScale = 0; yield return new WaitForSecondsRealtime(0.12f);
            Assert.IsFalse(a.activeSelf); Assert.AreEqual(0, pool.CountActive(template));
        }
        [UnityTest]
        public IEnumerator ScaledTimerDoesNotAdvanceDuringPause()
        {
            var a = pool.Spawn(template); pool.DespawnAfter(a, 0.05f); Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(0.1f); Assert.IsTrue(a.activeSelf);
            Time.timeScale = 1; yield return new WaitForSecondsRealtime(0.12f); Assert.IsFalse(a.activeSelf);
        }
        [UnityTest]
        public IEnumerator ExternalDestroyAndClearCanRecoverCapacity()
        {
            pool.Configure(template, 1); var a = pool.Spawn(template); Object.Destroy(a);
            yield return null; Assert.IsNotNull(pool.Spawn(template));
            pool.Clear(template); Assert.IsNull(pool.Spawn(template)); // Destroy is deferred until end of frame.
            yield return null; Assert.IsNotNull(pool.Spawn(template));
        }
        [UnityTest]
        public IEnumerator ClearInactiveLeavesActiveObjectAlone()
        {
            pool.Configure(template, 2, 2); var active = pool.Spawn(template);
            pool.ClearAll(false); yield return null;
            Assert.IsTrue(active.activeSelf); Assert.AreEqual(1, pool.CountAll(template));
        }
        [Test]
        public void DisableManagerReturnsItsObjects()
        {
            var a = pool.Spawn(template); pool.enabled = false;
            Assert.IsFalse(a.activeSelf); Assert.AreEqual(0, pool.CountActive(template));
            Assert.IsNull(pool.Spawn(template)); pool.enabled = true; Assert.AreSame(a, pool.Spawn(template));
        }
        [Test]
        public void InvalidInputsAreSafe()
        {
            Assert.IsNull(pool.Spawn(null)); Assert.IsFalse(pool.Despawn(null));
            Assert.IsFalse(pool.Configure(template, 0)); Assert.IsFalse(pool.Prewarm(template, -1));
            Assert.IsFalse(pool.DespawnAfter(template, 1));
        }
        [UnityTest]
        public IEnumerator DestroyingManagerAlsoDestroysInstancesWithExternalParents()
        {
            var parent = new GameObject("External parent");
            try
            {
                var a = pool.Spawn(template, parent: parent.transform);
                Object.Destroy(pool.gameObject); yield return null; yield return null;
                Assert.IsTrue(a == null);
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
