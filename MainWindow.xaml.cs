// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Text;
using Microsoft.Windows.AppNotifications.Builder;
using WK.Libraries.SharpClipboardNS;
using Microsoft.Windows.AppNotifications;
using System;
using Windows.Storage;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.ApplicationModel.DataTransfer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Cheatboard;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
	private readonly SharpClipboard _clipboard = new();
	private readonly HttpClient _httpClient = new();
	private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;
	private string LastAnswer { get; set; } = "";

	private readonly string _defaultPrompt =
		"Without any explanation, Write directly the correct answer(s) of the following question(s)";
	public MainWindow()
	{
		InitializeComponent();
        _clipboard.ClipboardChanged += ClipboardChanged;
		_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		var storedPrompt = (string)(_localSettings.Values["Prompt"] ??= _defaultPrompt);
        var storedOpenAiApiKey = (string)_localSettings.Values["OpenAiApiKey"];
        PromptEditBox.Document.SetText(TextSetOptions.None, storedPrompt);
        OpenAiApiKeyEditBox.Document.SetText(TextSetOptions.None, storedOpenAiApiKey);
    }
	
    private async void ClipboardChanged(object sender, SharpClipboard.ClipboardChangedEventArgs e)
	{
		if (!IsRunningSwitch.IsOn) return;
        if (e.ContentType != SharpClipboard.ContentTypes.Text)
            return;
		var text = _clipboard.ClipboardText;
		if (LastAnswer == text)
			return;

        XmlDocument badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeGlyph);
        XmlElement badgeElement = badgeXml.SelectSingleNode("/badge") as XmlElement;
        badgeElement.SetAttribute("value", "available");
        BadgeNotification badge = new BadgeNotification(badgeXml);
        BadgeUpdater badgeUpdater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();
        badgeUpdater.Update(badge);
        OpenAiApiKeyEditBox.Document.GetText(TextGetOptions.None, out var openAiKey);
		PromptEditBox.Document.GetText(TextGetOptions.None, out var prompt);
		if (string.IsNullOrWhiteSpace(openAiKey))
		{
			var missingApiKeyError = new AppNotificationBuilder()
				.MuteAudio()
				.AddText("Error: OpenAI API Key is missing")
				.BuildNotification();
			AppNotificationManager.Default.Show(missingApiKeyError);
		}
		if (string.IsNullOrWhiteSpace(prompt))
		{
			prompt = _defaultPrompt;
		}
		openAiKey = openAiKey.Replace("\r", string.Empty).Replace("\n", string.Empty);
		_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
			"Bearer",
			openAiKey);
		
		if (text == null) return;
		const int maxTokens = 256;
		const double temperature = 0.2;
		const string model = "gpt-3.5-turbo";
		try
		{
			var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", 
				new StringContent(JsonSerializer.Serialize(new
				{
					messages = new[]
					{
						new
						{
							role = "system",
							content = prompt
						},
						new
						{
							role = "user",
							content = text
						}
					},
					max_tokens = maxTokens,
					temperature,
					model
				}), Encoding.UTF8, "application/json"));
			var responseContent = await response.Content.ReadAsStringAsync();
			var completionResponse = JsonSerializer.Deserialize<CompletionResponse>(responseContent);
			var completion = completionResponse?.choices[0].message.content;
			if (completion == null) return;
			var notification = new AppNotificationBuilder()
				.MuteAudio()
				.AddText(completion)
				.BuildNotification();
			AppNotificationManager.Default.Show(notification);
            LastAnswer = completion;
            var package = new DataPackage();
			package.SetText(completion);
            Clipboard.SetContent(package);

        }
        catch (Exception)
        {
			var error = new AppNotificationBuilder()
				.MuteAudio()
				.AddText("Error: Check your API key and internet connection")
				.BuildNotification();
			AppNotificationManager.Default.Show(error);
		}
		finally
        {
            badgeUpdater.Clear();
        }
    }

	private void PromptEditBox_TextChanged(object sender, RoutedEventArgs e)
	{
		PromptEditBox.Document.GetText(TextGetOptions.None, out var value);
        value = value.Replace("\r", string.Empty).Replace("\n", string.Empty);
        _localSettings.Values["Prompt"] = value;
	}

    private void OpenAiApiKeyEditBox_TextChanged(object sender, RoutedEventArgs e)
    {
        OpenAiApiKeyEditBox.Document.GetText(TextGetOptions.None, out var value);
        value = value.Replace("\r", string.Empty).Replace("\n", string.Empty);
        _localSettings.Values["OpenAiApiKey"] = value;
    }
}
