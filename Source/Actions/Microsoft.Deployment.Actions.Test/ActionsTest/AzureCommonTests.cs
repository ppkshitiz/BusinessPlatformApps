﻿using Microsoft.Deployment.Actions.Test.TestHelpers;
using Microsoft.Deployment.Common;
using Microsoft.Deployment.Common.ActionModel;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hyak.Common;
using Microsoft.Azure;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;
using Microsoft.Azure.Subscriptions;
using Microsoft.Azure.Subscriptions.Models;
using Microsoft.Deployment.Common.Helpers;

namespace Microsoft.Deployment.Actions.Test.ActionsTest
{
    [TestClass]
    public class AzureCommonTests
    {
        [TestMethod]
        public async Task GetAzureToken()
        {
            DataStore dataStore = new DataStore();
            var datastore = await AAD.GetTokenWithDataStore();
            var result = await TestHarness.ExecuteActionAsync("Microsoft-GetAzureSubscriptions", datastore);
            Assert.IsTrue(result.IsSuccess);
            var responseBody = JObject.FromObject(result.Body);
        }

        [Ignore]
        [TestMethod]
        public async Task GetAzureTokenAndRefresh()
        {
            string ticks = "1480723773";
            dynamic tokenObj = new ExpandoObject();
            tokenObj.access_token = "FAKE";
            tokenObj.expires_on = ticks;
            DataStore datastore = new DataStore();
            datastore.AddToDataStore("AzureToken", JObject.FromObject(tokenObj), DataStoreType.Private);

            var result = await TestHarness.ExecuteActionAsync("Microsoft-GetAzureSubscriptions", datastore);
        }

        [Ignore]
        [TestMethod]
        public async Task DeployArmTemplateTest()
        {
            var datastore = await TestHarness.GetCommonDataStore();
            datastore.AddToDataStore("AzureArmFile", "Service/Arm/armtemplate.json");
            var paramFile = JsonUtility.GetJsonObjectFromJsonString(System.IO.File.ReadAllText(@"Apps/TestApps/TestApp/Service/Arm/armparam.json"));
            paramFile["AzureArmParameters"]["SqlServerName"] = "sqltestserver" + RandomGenerator.GetRandomLowerCaseCharacters(10);
            datastore.AddToDataStore("AzureArmParameters", paramFile["AzureArmParameters"]);
            var armResult = await TestHarness.ExecuteActionAsync("Microsoft-DeployAzureArmTemplate", datastore);
            Assert.IsTrue(armResult.IsSuccess);
        }

        [TestMethod]
        public async Task DeleteResourceGroups()
        {
            DataStore dataStore = await AAD.GetUserTokenFromPopup();

            CloudCredentials creds = new TokenCloudCredentials(dataStore.GetJson("AzureToken")["access_token"].ToString());
            Microsoft.Azure.Subscriptions.SubscriptionClient clientSubscription = new SubscriptionClient(creds);
            var subscriptionList = (await clientSubscription.Subscriptions.ListAsync(new CancellationToken())).Subscriptions.ToList();
            List<Task> tasksToExecute = new List<Task>();

            foreach (var subscription in subscriptionList)
            {
                SubscriptionCloudCredentials creds2 = new TokenCloudCredentials(subscription.SubscriptionId, dataStore.GetJson("AzureToken")["access_token"].ToString());
                Microsoft.Azure.Management.Resources.ResourceManagementClient client = new ResourceManagementClient(creds2);
                string subId = subscription.SubscriptionId;
                var resourceGroups = await client.ResourceGroups.ListAsync(new ResourceGroupListParameters());
                var resourceGroupsToDelete = resourceGroups.ResourceGroups.Where(p => p.Name.ToLower().StartsWith("solution"));
                foreach (var rg in resourceGroupsToDelete)
                {
                    tasksToExecute.Add(client.ResourceGroups.DeleteAsync(rg.Name, new CancellationToken()));
                }

                resourceGroupsToDelete = resourceGroups.ResourceGroups.Where(p => p.Name.ToLower().StartsWith("unit"));
                foreach (var rg in resourceGroupsToDelete)
                {
                    tasksToExecute.Add(client.ResourceGroups.DeleteAsync(rg.Name, new CancellationToken()));
                }

            }

            await Task.WhenAll(tasksToExecute);
        }
    }
}
