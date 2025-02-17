using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Files.App.DataModels;
using Files.App.Helpers;
using Files.Shared.Enums;
using Files.Shared.Extensions;
using Files.App.Extensions;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.Storage;
using System.Diagnostics.CodeAnalysis;

namespace Files.App.ViewModels
{
	[Obsolete("Do not use this class as Settings store anymore, settings have been merged to IUserSettingsService.")]
	public class SettingsViewModel : ObservableObject
	{
		private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

		public SettingsViewModel()
		{
			UpdateThemeElements = new RelayCommand(() => ThemeModeChanged?.Invoke(this, EventArgs.Empty));
		}

		#region Appearance

		/// <summary>
		/// Gets or sets the user's current selected skin
		/// </summary>
		public AppTheme SelectedTheme
		{
			get => JsonSerializer.Deserialize<AppTheme>(Get(JsonSerializer.Serialize(new AppTheme() { Name = "Default".GetLocalizedResource() })));
			set => Set(JsonSerializer.Serialize(value));
		}

		#endregion Appearance

		public event EventHandler ThemeModeChanged;

		public ICommand UpdateThemeElements { get; }

		#region ReadAndSaveSettings

		public bool Set<TValue>(TValue value, [CallerMemberName] string propertyName = null)
		{
			propertyName = propertyName != null && propertyName.StartsWith("set_", StringComparison.OrdinalIgnoreCase)
				? propertyName.Substring(4)
				: propertyName;

			TValue originalValue = default;

			if (localSettings.Values.ContainsKey(propertyName))
			{
				originalValue = Get(originalValue, propertyName);

				localSettings.Values[propertyName] = value;
				if (!SetProperty(ref originalValue, value, propertyName))
				{
					return false;
				}
			}
			else
			{
				localSettings.Values[propertyName] = value;
			}

			return true;
		}

		public TValue Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]TValue>(TValue defaultValue, [CallerMemberName] string propertyName = null)
		{
			var name = propertyName ??
					   throw new ArgumentNullException(nameof(propertyName), "Cannot store property of unnamed.");

			name = name.StartsWith("get_", StringComparison.OrdinalIgnoreCase)
				? propertyName.Substring(4)
				: propertyName;

			if (localSettings.Values.ContainsKey(name))
			{
				var value = localSettings.Values[name];

				if (value is not TValue tValue)
				{
					if (value is IConvertible)
					{
						tValue = (TValue)Convert.ChangeType(value, typeof(TValue));
					}
					else
					{
						var valueType = value.GetType();
						var tryParse = typeof(TValue).GetMethod("TryParse", BindingFlags.Instance | BindingFlags.Public);

						if (tryParse == null)
						{
							return default;
						}

						var stringValue = value.ToString();
						tValue = default;

						var tryParseDelegate =
							(TryParseDelegate<TValue>)Delegate.CreateDelegate(valueType, tryParse, false);

						tValue = (tryParseDelegate?.Invoke(stringValue, out tValue) ?? false) ? tValue : default;
					}

					Set(tValue, propertyName); // Put the corrected value in settings.
					return tValue;
				}
				return tValue;
			}

			localSettings.Values[propertyName] = defaultValue;

			return defaultValue;
		}

		private delegate bool TryParseDelegate<TValue>(string inValue, out TValue parsedValue);

		#endregion ReadAndSaveSettings
	}
}
