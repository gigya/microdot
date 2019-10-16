using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using NUnit.Framework;

namespace DataAnnotationsValidator.Tests
{
    [TestFixture]
    public class DataAnnotationsValidatorTests
    {
        private IDataAnnotationsValidator _validator;

        [SetUp]
        public void Setup()
        {
            SaveValidationContextAttribute.SavedContexts.Clear();
            _validator = new DataAnnotationsValidator();
        }

        [Test]
        public void TryValidateObject_on_valid_parent_returns_no_errors()
        {
            var parent = new Parent { PropertyA = 1, PropertyB = 1 };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObject(parent, validationResults);

            Assert.IsTrue(result);
            Assert.AreEqual(0, validationResults.Count);
        }

        [Test]
        public void TryValidateObject_when_missing_required_properties_returns_errors()
        {
            var parent = new Parent { PropertyA = null, PropertyB = null };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObject(parent, validationResults);

            Assert.IsFalse(result);
            Assert.AreEqual(2, validationResults.Count);
            Assert.AreEqual(1, validationResults.Count(x => x.ErrorMessage == "Parent PropertyA is required"));
            Assert.AreEqual(1, validationResults.Count(x => x.ErrorMessage == "Parent PropertyB is required"));
        }

        [Test]
        public void TryValidateObject_calls_IValidatableObject_method()
        {
            var parent = new Parent { PropertyA = 5, PropertyB = 6 };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObject(parent, validationResults);

            Assert.IsFalse(result);
            Assert.AreEqual(1, validationResults.Count);
            Assert.AreEqual("Parent PropertyA and PropertyB cannot add up to more than 10", validationResults[0].ErrorMessage);
        }

        [Test]
        public void TryValidateObjectRecursive_returns_errors_when_child_class_has_invalid_properties()
        {
            var parent = new Parent { PropertyA = 1, PropertyB = 1 };
            parent.Child = new Child { Parent = parent, PropertyA = null, PropertyB = 5 };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObjectRecursive(parent, validationResults);

            Assert.IsFalse(result);
            Assert.AreEqual(1, validationResults.Count);
            Assert.AreEqual("Child PropertyA is required", validationResults[0].ErrorMessage);
        }

        [Test]
        public void TryValidateObjectRecursive_ignored_errors_when_child_class_has_SkipRecursiveValidationProperty()
        {
            var parent = new Parent { PropertyA = 1, PropertyB = 1 };
            parent.Child = new Child { Parent = parent, PropertyA = 1, PropertyB = 1 };
            parent.SkippedChild = new Child { PropertyA = null, PropertyB = 1 };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObjectRecursive(parent, validationResults);

            Assert.IsTrue(result);
        }

        [Test]
        public void TryValidateObjectRecursive_calls_IValidatableObject_method_on_child_class()
        {
            var parent = new Parent { PropertyA = 1, PropertyB = 1 };
            parent.Child = new Child { Parent = parent, PropertyA = 5, PropertyB = 6 };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObjectRecursive(parent, validationResults);

            Assert.IsFalse(result);
            Assert.AreEqual(1, validationResults.Count);
            Assert.AreEqual("Child PropertyA and PropertyB cannot add up to more than 10", validationResults[0].ErrorMessage);
        }

        [Test]
        public void TryValidateObjectRecursive_returns_errors_when_grandchild_class_has_invalid_properties()
        {
            var parent = new Parent { PropertyA = 1, PropertyB = 1 };
            parent.Child = new Child
            {
                Parent = parent,
                PropertyA = 1,
                PropertyB = 1,
                GrandChildren = new[] {new GrandChild {PropertyA = 11, PropertyB = 11}}
            };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObjectRecursive(parent, validationResults);

            Assert.IsFalse(result);
            Assert.AreEqual(2, validationResults.Count);
            Assert.AreEqual(1, validationResults.Count(x => x.ErrorMessage == "GrandChild PropertyA not within range"));
            Assert.AreEqual(1, validationResults.Count(x => x.ErrorMessage == "GrandChild PropertyB not within range"));
        }

        [Test]
        public void TryValidateObjectRecursive_passes_validation_context_items_to_all_validation_calls()
        {
            var parent = new Parent {Child = new Child {GrandChildren = new[] {new GrandChild()}}};
            var validationResults = new List<ValidationResult>();

            var contextItems = new Dictionary<object, object> { { "key", 12345 } };

            _validator.TryValidateObjectRecursive(parent, validationResults, contextItems);

            Assert.AreEqual(3, SaveValidationContextAttribute.SavedContexts.Count, "Test expects 3 validated properties in the object graph to have a SaveValidationContextAttribute");
            Assert.That(SaveValidationContextAttribute.SavedContexts.Select(c => c.Items).All(items => items["key"] == contextItems["key"]));
        }

        [Test]
        public void TryValidateObject_calls_grandchild_IValidatableObject_method()
        {
            var parent = new Parent { PropertyA = 1, PropertyB = 1 };
            parent.Child = new Child
            {
                Parent = parent,
                PropertyA = 1,
                PropertyB = 1,
                GrandChildren = new[] {new GrandChild {PropertyA = 5, PropertyB = 6}}
            };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObjectRecursive(parent, validationResults);

            Assert.IsFalse(result);
            Assert.AreEqual(1, validationResults.Count);
            Assert.AreEqual(1, validationResults.Count(x => x.ErrorMessage == "GrandChild PropertyA and PropertyB cannot add up to more than 10"));
        }

        [Test]
        public void TryValidateObject_includes_errors_from_all_objects()
        {
            var parent = new Parent { PropertyA = 5, PropertyB = 6 };
            parent.Child = new Child
            {
                Parent = parent,
                PropertyA = 5,
                PropertyB = 6,
                GrandChildren = new[] {new GrandChild {PropertyA = 5, PropertyB = 6}}
            };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObjectRecursive(parent, validationResults);

            Assert.IsFalse(result);
            Assert.AreEqual(3, validationResults.Count);
            Assert.AreEqual(1, validationResults.Count(x => x.ErrorMessage == "Parent PropertyA and PropertyB cannot add up to more than 10"));
            Assert.AreEqual(1, validationResults.Count(x => x.ErrorMessage == "Child PropertyA and PropertyB cannot add up to more than 10"));
            Assert.AreEqual(1, validationResults.Count(x => x.ErrorMessage == "GrandChild PropertyA and PropertyB cannot add up to more than 10"));
        }

        [Test]
        public void TryValidateObject_modifies_membernames_for_nested_properties()
        {
            var parent = new Parent { PropertyA = 1, PropertyB = 1 };
            parent.Child = new Child { Parent = parent, PropertyA = null, PropertyB = 5 };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObjectRecursive(parent, validationResults);

            Assert.IsFalse(result);
            Assert.AreEqual(1, validationResults.Count);
            Assert.AreEqual("Child PropertyA is required", validationResults[0].ErrorMessage);
            Assert.AreEqual("Child.PropertyA", validationResults[0].MemberNames.First());
        }

        [Test]
        public void TryValidateObject_object_with_dictionary_does_not_fail()
        {
            var parent = new Parent { PropertyA = 1, PropertyB = 1 };
            var classWithDictionary = new ClassWithDictionary
            {
                Objects = new List<Dictionary<string, Child>>
                {
                    new Dictionary<string, Child>
                    {
                        { "key",
                            new Child
                            {
                                Parent = parent,
                                PropertyA = 1,
                                PropertyB = 2
                            }
                        }
                    }
                }
            };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObjectRecursive(classWithDictionary, validationResults);

            Assert.IsTrue(result);
            Assert.IsEmpty(validationResults);
        }

        [Test]
        public void TryValidateObject_object_with_null_enumeration_values_does_not_fail()
        {
            var parent = new Parent { PropertyA = 1, PropertyB = 1 };
            var classWithNullableEnumeration = new ClassWithNullableEnumeration
            {
                Objects = new List<Child>
                {
                    null,
                    new Child
                    {
                        Parent = parent,
                        PropertyA = 1,
                        PropertyB = 2
                    }
                }
            };
            var validationResults = new List<ValidationResult>();

            var result = _validator.TryValidateObjectRecursive(classWithNullableEnumeration, validationResults);

            Assert.IsTrue(result);
            Assert.IsEmpty(validationResults);
        }
 
    }
}
