using NUnit.Framework;

namespace AbyssMoth.Tests.EditMode
{
    public class ServiceContainerTests
    {
        [Test]
        public void Add_And_Get_ReturnsRegisteredService()
        {
            var container = new ServiceContainer();
            var service = new TestService();

            container.Add(service);

            Assert.That(container.TryGet(out TestService resolved), Is.True);
            Assert.That(resolved, Is.SameAs(service));
            Assert.That(container.Get<TestService>(), Is.SameAs(service));
        }

        [Test]
        public void AddOrThrow_ThrowsOnDuplicate()
        {
            var container = new ServiceContainer();

            container.AddOrThrow(new TestService());

            Assert.Throws<System.InvalidOperationException>(() =>
                container.AddOrThrow(new TestService()));
        }

        [Test]
        public void TaggedServices_ResolveFromParentContainer()
        {
            var parent = new ServiceContainer();
            var child = new ServiceContainer(parent);

            var service = new TestService();
            parent.AddTagged("Hero", service);

            Assert.That(child.TryGetTagged("Hero", out TestService resolved), Is.True);
            Assert.That(resolved, Is.SameAs(service));
        }

        [Test]
        public void TaggedServices_ContainsGetAndRemove_WorkAsExpected()
        {
            var container = new ServiceContainer();
            var service = new TestService();

            container.AddTagged("Hero", service);

            Assert.That(container.ContainsTagged<TestService>("Hero"), Is.True);
            Assert.That(container.GetTagged<TestService>("Hero"), Is.SameAs(service));

            Assert.That(container.RemoveTagged<TestService>("Hero"), Is.True);
            Assert.That(container.ContainsTagged<TestService>("Hero"), Is.False);
            Assert.That(container.TryGetTagged("Hero", out TestService _), Is.False);
        }

        [Test]
        public void AddTagged_WithOverwriteFalse_ThrowsOnDuplicateTag()
        {
            var container = new ServiceContainer();

            container.AddTagged("Hero", new TestService(), overwrite: false);

            Assert.Throws<System.InvalidOperationException>(() =>
                container.AddTagged("Hero", new TestService(), overwrite: false));
        }

        [Test]
        public void RemoveIfSame_RemovesOnlyMatchingInstance()
        {
            var container = new ServiceContainer();
            var expected = new TestService();
            var another = new TestService();

            container.Add(expected);

            Assert.That(container.RemoveIfSame(another), Is.False);
            Assert.That(container.TryGet(out TestService stillThere), Is.True);
            Assert.That(stillThere, Is.SameAs(expected));

            Assert.That(container.RemoveIfSame(expected), Is.True);
            Assert.That(container.TryGet(out TestService _), Is.False);
        }

        private sealed class TestService { }
    }
}
