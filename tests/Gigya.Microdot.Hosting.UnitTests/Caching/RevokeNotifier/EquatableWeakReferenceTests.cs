using Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;

namespace Gigya.Microdot.Hosting.UnitTests.Caching.RevokeNotifier
{
    [TestFixture]
    public class EquatableWeakReferenceTests
    {
        [SetUp]
        public void SetUp()
        {
            
        }

        private EquatableWeakReference<RevokeNotifierTestClass> CreateEquatableWeakReference(bool collectObject = false)
        {
            EquatableWeakReference<RevokeNotifierTestClass> res = null;
            Action notRootByDebuger = () =>
            {
                res = new EquatableWeakReference<RevokeNotifierTestClass>(new RevokeNotifierTestClass());

            };
            notRootByDebuger();
            if (collectObject)
            {
                RevokeNotifierTestClass.CallGC();
            }
            return res;
        }

        [Test]
        public void Test_Object_Is_Collected()
        {
            //Arrange
            var wr = CreateEquatableWeakReference(false);

            //Act
            RevokeNotifierTestClass.CallGC();

            //Assert
            Assert.Null(wr.Target);
        }

        private ConcurrentBag<RevokeNotifierTestClass> _anchor = new ConcurrentBag<RevokeNotifierTestClass>();

        [Test]
        public void Test_Object_Is_Not_Collected()
        {
            //Arrange
            var target = new RevokeNotifierTestClass();
            _anchor.Add(target);
            var wr = new EquatableWeakReference<RevokeNotifierTestClass>(target);

            //Act
            RevokeNotifierTestClass.CallGC();

            //Assert
            Assert.NotNull(wr.Target);
        }

        [Test]
        public void Test_Object_Is_Null_Throws()
        {
            //Arrange
            RevokeNotifierTestClass target = null;

            //Act & Assert
            Assert.Throws<NullReferenceException>(() => new EquatableWeakReference<RevokeNotifierTestClass>(target));
        }

        [Test]
        public void Equals_EquatableWeakReference_Is_Not_Equal_Null()
        {
            // Arrange
            var wr = CreateEquatableWeakReference(true);
            EquatableWeakReference<RevokeNotifierTestClass> other = null;

            // Act
            var result = wr.Equals(other);

            // Assert
            Assert.IsNull(wr.Target);
            Assert.False(result);
        }

        [Test]
        public void Equals_EquatableWeakReference_Are_Reference_Equal()
        {
            // Arrange
            var target = new RevokeNotifierTestClass();
            var wr1 = new EquatableWeakReference<RevokeNotifierTestClass>(target);
            var wr2 = new EquatableWeakReference<RevokeNotifierTestClass>(target);

            // Act
            var result = wr1.Equals(wr2);

            // Assert
            Assert.True(result);
        }

        [Test]
        public void Equals_EquatableWeakReference_Are_Not_Equal()
        {
            // Arrange
            var target1 = new RevokeNotifierTestClass();
            var target2 = new RevokeNotifierTestClass();
            var wr1 = new EquatableWeakReference<RevokeNotifierTestClass>(target1);
            var wr2 = new EquatableWeakReference<RevokeNotifierTestClass>(target2);

            // Act
            var result1 = wr1.Equals(wr2);
            var result2 = target1.Equals(target2);

            // Assert
            Assert.False(result1);
            Assert.True(result2);
        }

        [Test]
        public void Equals_EquatableWeakReference_Are_Not_Equal_Even_If_HashCode_is_And_Target_collected()
        {
            // Arrange
            var wr = CreateEquatableWeakReference(true);
            var target = new RevokeNotifierTestClass();
            var wr2 = new EquatableWeakReference<RevokeNotifierTestClass>(target);

            // Act
            var result1 = wr2.Equals(wr);

            // Assert
            Assert.Null(wr.Target);
            Assert.AreEqual(wr.GetHashCode(),wr2.GetHashCode());
            Assert.AreNotEqual(wr, wr2);
        }

        [Test]
        public void GetHashCode_EquatableWeakReference_Equals_Target_HashCode()
        {
            // Arrange
            var target = new RevokeNotifierTestClass();
            var wr = new EquatableWeakReference<RevokeNotifierTestClass>(target);

            // Act
            var result = wr.GetHashCode();

            // Assert
            Assert.AreEqual(result, target.GetHashCode());
        }

        //
        [Test]
        public void GetHashCode_EquatableWeakReference_Equals_Target_HashCode_After_Been_collected()
        {
            // Arrange
            var wr = CreateEquatableWeakReference(true);

            // Act
            var result = wr.GetHashCode();

            // Assert
            Assert.Null(wr.Target);
            Assert.AreEqual(result, 1234);
        }
    }
}
