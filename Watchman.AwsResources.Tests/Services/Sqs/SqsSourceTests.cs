using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Moq;
using NUnit.Framework;
using Watchman.AwsResources.Services.Sqs;

namespace Watchman.AwsResources.Tests.Services.Sqs
{
    [TestFixture]
    public class SqsSourceTests
    {
        private ListMetricsResponse _firstPage;
        private ListMetricsResponse _secondPage;
        private ListMetricsResponse _thirdPage;
        private ListMetricsResponse _fourthPage;
        private Dictionary<string, string> _attributes;

        private QueueSource _queueSource;

        [SetUp]
        public void Setup()
        {
            _firstPage = new ListMetricsResponse
            {
                NextToken = "token-1",
                Metrics = new List<Metric>
                {
                    new Metric
                    {
                        MetricName = "ApproximateAgeOfOldestMessage",
                        Dimensions = new List<Dimension>
                        {
                            new Dimension
                            {
                                Name = "QueueName",
                                Value = "Queue-1"
                            }
                        }
                    }
                }
            };
            _secondPage = new ListMetricsResponse
            {
                NextToken = "token-2",
                Metrics = new List<Metric>
                {
                    new Metric
                    {
                        MetricName = "ApproximateAgeOfOldestMessage",
                        Dimensions = new List<Dimension>
                        {
                            new Dimension
                            {
                                Name = "QueueName",
                                Value = "Queue-2"
                            }
                        }
                    }
                }
            };
            _thirdPage = new ListMetricsResponse
            {
                NextToken = "token-3",
                Metrics = new List<Metric>
                {
                    new Metric
                    {
                        MetricName = "ApproximateAgeOfOldestMessage",
                        Dimensions = new List<Dimension>
                        {
                            new Dimension
                            {
                                Name = "QueueName",
                                Value = "Queue-3"
                            }
                        }
                    }
                }
            };

            _fourthPage = new ListMetricsResponse
            {
                Metrics = new List<Metric>
                {
                    new Metric
                    {
                        MetricName = "ApproximateAgeOfOldestMessage",
                        Dimensions = new List<Dimension>
                        {
                            new Dimension
                            {
                                Name = "QueueName",
                                Value = "Queue-4_error"
                            }
                        }
                    }
                }
            };

            _attributes = new Dictionary<string, string>
            {
                {"AttrName", "AttrValue"}
            };

            var cloudWatchMock = new Mock<IAmazonCloudWatch>();
            cloudWatchMock.Setup(s => s.ListMetricsAsync(
                It.Is<ListMetricsRequest>(r => r.MetricName == "ApproximateAgeOfOldestMessage" && r.NextToken == null),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(_firstPage);

            cloudWatchMock.Setup(s => s.ListMetricsAsync(
                It.Is<ListMetricsRequest>(r => r.MetricName == "ApproximateAgeOfOldestMessage" && r.NextToken == "token-1"),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(_secondPage);

            cloudWatchMock.Setup(s => s.ListMetricsAsync(
                It.Is<ListMetricsRequest>(r => r.MetricName == "ApproximateAgeOfOldestMessage" && r.NextToken == "token-2"),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(_thirdPage);

            cloudWatchMock.Setup(s => s.ListMetricsAsync(
                It.Is<ListMetricsRequest>(r => r.MetricName == "ApproximateAgeOfOldestMessage" && r.NextToken == "token-3"),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(_fourthPage);

            _queueSource = new QueueSource(cloudWatchMock.Object);
        }

        [Test]
        public async Task GetResourcesAsync_MultiplePages_AllFetchedAndReturned()
        {
            // arrange

            // act
            var result = await _queueSource.GetResourceNamesAsync();

            // assert
            Assert.That(result.Count, Is.EqualTo(4));

            Assert.That(result.First(), Is.EqualTo(_firstPage.Metrics.Single().Dimensions.Single().Value));
            Assert.That(result.Skip(1).First(), Is.EqualTo(_secondPage.Metrics.Single().Dimensions.Single().Value));
            Assert.That(result.Skip(2).First(), Is.EqualTo(_thirdPage.Metrics.Single().Dimensions.Single().Value));
            Assert.That(result.Skip(3).First(), Is.EqualTo(_fourthPage.Metrics.Single().Dimensions.Single().Value));
        }

        [Test]
        public async Task GetResourcesAsync_SinglePage_FetchedAndReturned()
        {
            // arrange
            _firstPage.NextToken = null;

            // act
            var result = await _queueSource.GetResourceNamesAsync();

            // assert
            Assert.That(result.Count, Is.EqualTo(1));

            Assert.That(result.First(), Is.EqualTo(_firstPage.Metrics.Single().Dimensions.Single().Value));
        }

        [Test]
        public async Task GetResourcesAsync_EmptyResult_EmptyListReturned()
        {
            // arrange
            _firstPage.NextToken = null;
            _firstPage.Metrics = new List<Metric>();

            // act
            var result = await _queueSource.GetResourceNamesAsync();

            // assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task GetResourceAsync_ReturnsCorrectResource()
        {
            // arrange
            var secondQueueName = _secondPage.Metrics.First().Dimensions.Single().Value;

            // act
            var result = await _queueSource.GetResourceAsync(secondQueueName);

            // assert
            Assert.That(result.Name, Is.EqualTo(secondQueueName));
            Assert.That(result.Resource, Is.InstanceOf<QueueData>());
            Assert.That(result.Resource.Name, Is.EqualTo(secondQueueName));
        }

        [Test]
        public async Task GetResourceAsyncCorrectlyPopulatesTheIsErrorField()
        {
            // arrange
            var fourthQueueName = _fourthPage.Metrics.First().Dimensions.Single().Value;

            // act
            var result = await _queueSource.GetResourceAsync(fourthQueueName);

            // assert
            Assert.That(result.Resource.IsErrorQueue, Is.True);
        }
    }
}
