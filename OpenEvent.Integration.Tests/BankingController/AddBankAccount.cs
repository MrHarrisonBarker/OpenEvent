using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using OpenEvent.Data.Models.BankAccount;

namespace OpenEvent.Integration.Tests.BankingController
{
    [TestFixture]
    public class AddBankAccount
    {
        private HttpClient Client;

        [SetUp]
        public void Setup()
        {
            Client = ClientFactory.Create();
        }

        [Test]
        public async Task Should_Add_Bank()
        {
            var loggedUser = await TestData.LogUserIn(Client);

            AddBankAccountBody body = new()
            {
                UserId = loggedUser.Id,
                BankToken = "btok_us_verified"
            };
            
            var response = await Client.PostAsJsonAsync(TestData.BaseUrl + "/api/banking/AddBankAccount",body);
            response.StatusCode.Should().Be(200);
            
            var bankString = await response.Content.ReadAsStringAsync();
            var bank = JsonConvert.DeserializeObject<BankAccountViewModel>(bankString);
            
            bank.Bank.Should().BeEquivalentTo("STRIPE TEST BANK");
        }
    }
}