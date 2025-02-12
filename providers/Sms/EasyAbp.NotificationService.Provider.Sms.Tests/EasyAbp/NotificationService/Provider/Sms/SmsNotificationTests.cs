using EasyAbp.NotificationService.NotificationInfos;
using EasyAbp.NotificationService.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Json;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace EasyAbp.NotificationService.Provider.Sms
{
    public class SmsNotificationTests : NotificationServiceTestBase<NotificationServiceProviderSmsTestsModule>
    {
        private const string Text = "test";
        private const string ExtraPropertyKey = "MyProperty";
        private const string ExtraPropertyValue = "123456";

        protected ICurrentTenant CurrentTenant { get; set; }
        protected IJsonSerializer JsonSerializer { get; set; }
        protected INotificationRepository NotificationRepository { get; set; }
        protected INotificationInfoRepository NotificationInfoRepository { get; set; }
        protected IAsyncBackgroundJob<SmsNotificationSendingJobArgs> SmsNotificationSendingJob { get; set; }

        public SmsNotificationTests()
        {
            CurrentTenant = ServiceProvider.GetRequiredService<ICurrentTenant>();
            JsonSerializer = ServiceProvider.GetRequiredService<IJsonSerializer>();
            NotificationRepository = ServiceProvider.GetRequiredService<INotificationRepository>();
            NotificationInfoRepository = ServiceProvider.GetRequiredService<INotificationInfoRepository>();
            SmsNotificationSendingJob =
                ServiceProvider.GetRequiredService<IAsyncBackgroundJob<SmsNotificationSendingJobArgs>>();
        }

        [Fact]
        public async Task Should_Create_Notifications()
        {
            var userIds = new List<Guid>
            {
                NotificationServiceProviderSmsTestConsts.FakeUser1Id,
                NotificationServiceProviderSmsTestConsts.FakeUser2Id
            };

            await CreateSmsNotificationAsync(userIds, Text, new Dictionary<string, object>
            {
                { ExtraPropertyKey, ExtraPropertyValue }
            });

            var notifications = await NotificationRepository.GetListAsync();

            notifications.Count.ShouldBe(2);

            foreach (var notification in userIds.Select(userId => notifications.Find(x => x.UserId == userId)))
            {
                notification.ShouldNotBeNull();
            }

            var notificationInfo = await NotificationInfoRepository.GetAsync(notifications.First().NotificationInfoId);

            var text = notificationInfo.GetSmsText();

            var properties =
                JsonSerializer.Deserialize<IDictionary<string, object>>(notificationInfo.GetSmsJsonProperties());

            text.ShouldBe(Text);
            properties.Count.ShouldBe(1);
            properties.First().Key.ShouldBe(ExtraPropertyKey);
            properties.First().Value.ShouldBe(ExtraPropertyValue);
        }

        private async Task CreateSmsNotificationAsync(List<Guid> userIds, string text,
            IDictionary<string, object> properties)
        {
            var handler = ServiceProvider.GetRequiredService<CreateSmsNotificationEventHandler>();

            var eto = new CreateSmsNotificationEto(CurrentTenant.Id, userIds, text, properties, JsonSerializer);

            await handler.HandleEventAsync(eto);
        }

        [Fact]
        public async Task Should_Set_Notification_Result_To_Success()
        {
            var userIds = new List<Guid>
            {
                NotificationServiceProviderSmsTestConsts.FakeUser1Id
            };

            await CreateSmsNotificationAsync(userIds, Text, new Dictionary<string, object>
            {
                { ExtraPropertyKey, ExtraPropertyValue }
            });

            var notification = (await NotificationRepository.GetListAsync()).First();

            await SmsNotificationSendingJob.ExecuteAsync(
                new SmsNotificationSendingJobArgs(notification.TenantId, notification.Id));

            notification = await NotificationRepository.GetAsync(notification.Id);

            notification.Success.ShouldBe(true);
            notification.CompletionTime.ShouldNotBeNull();
            notification.FailureReason.ShouldBeNull();
        }

        [Fact]
        public async Task Should_Set_Notification_Result_To_Failure_If_Phone_Number_Is_Not_Confirmed()
        {
            var userIds = new List<Guid>
            {
                NotificationServiceProviderSmsTestConsts.FakeUser2Id
            };

            await CreateSmsNotificationAsync(userIds, Text, new Dictionary<string, object>
            {
                { ExtraPropertyKey, ExtraPropertyValue }
            });

            var notification = (await NotificationRepository.GetListAsync()).First();

            await SmsNotificationSendingJob.ExecuteAsync(
                new SmsNotificationSendingJobArgs(notification.TenantId, notification.Id));

            notification = await NotificationRepository.GetAsync(notification.Id);

            notification.Success.ShouldBe(false);
            notification.CompletionTime.ShouldNotBeNull();
            notification.FailureReason.ShouldBe(NotificationConsts.FailureReasons.ReceiverInfoNotFound);
        }

        [Fact]
        public async Task Should_Set_Notification_Result_To_Failure_If_Phone_Number_Is_Not_Set()
        {
            var userIds = new List<Guid>
            {
                NotificationServiceProviderSmsTestConsts.FakeUser3Id
            };

            await CreateSmsNotificationAsync(userIds, Text, new Dictionary<string, object>
            {
                { ExtraPropertyKey, ExtraPropertyValue }
            });

            var notification = (await NotificationRepository.GetListAsync()).First();

            await SmsNotificationSendingJob.ExecuteAsync(
                new SmsNotificationSendingJobArgs(notification.TenantId, notification.Id));

            notification = await NotificationRepository.GetAsync(notification.Id);

            notification.Success.ShouldBe(false);
            notification.CompletionTime.ShouldNotBeNull();
            notification.FailureReason.ShouldBe(NotificationConsts.FailureReasons.ReceiverInfoNotFound);
        }

        [Fact]
        public async Task Should_Set_Notification_Result_To_Failure_If_User_Not_Found()
        {
            var userIds = new List<Guid>
            {
                Guid.NewGuid()
            };

            await CreateSmsNotificationAsync(userIds, Text, new Dictionary<string, object>
            {
                { ExtraPropertyKey, ExtraPropertyValue }
            });

            var notification = (await NotificationRepository.GetListAsync()).First();

            await SmsNotificationSendingJob.ExecuteAsync(
                new SmsNotificationSendingJobArgs(notification.TenantId, notification.Id));

            notification = await NotificationRepository.GetAsync(notification.Id);

            notification.Success.ShouldBe(false);
            notification.CompletionTime.ShouldNotBeNull();
            notification.FailureReason.ShouldBe(NotificationConsts.FailureReasons.ReceiverInfoNotFound);
        }
    }
}