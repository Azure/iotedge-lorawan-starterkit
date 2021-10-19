namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using System.Linq;
    using LoRaTools.Utils;
    using Xunit;

    public class CollectionExtensionsTests
    {
        [Fact]
        public void AddRange_SuccessCase()
        {
            // arrange
            var collection = Enumerable.Range(0, 5).ToList();

            // act
            CollectionExtensions.AddRange(collection, Enumerable.Range(5, 5));

            // assert
            Assert.Equal(Enumerable.Range(0, 10), collection);
        }

        [Fact]
        public void AddRange_ArgumentValidation()
        {
            // arrange + act
            static void firstArgMissing()
            {
                CollectionExtensions.AddRange(null, new[] { 1 });
            }

            static void secondArgMissing()
            {
                CollectionExtensions.AddRange(new[] { 1 }, null);
            }

            // assert
            _ = Assert.Throws<ArgumentNullException>(firstArgMissing);
            _ = Assert.Throws<ArgumentNullException>(secondArgMissing);
        }

        [Fact]
        public void ResetTo_SuccessCase()
        {
            // arrange
            var collection = Enumerable.Range(0, 5).ToList();

            // act
            CollectionExtensions.ResetTo(collection, Enumerable.Range(5, 5));

            // assert
            Assert.Equal(Enumerable.Range(5, 5), collection);
        }

        [Fact]
        public void ResetTo_ArgumentValidation()
        {
            // arrange + act
            static void firstArgMissing()
            {
                CollectionExtensions.ResetTo(null, new[] { 1 });
            }

            static void secondArgMissing()
            {
                CollectionExtensions.ResetTo(new[] { 1 }, null);
            }

            // assert
            _ = Assert.Throws<ArgumentNullException>(firstArgMissing);
            _ = Assert.Throws<ArgumentNullException>(secondArgMissing);
        }
    }
}
