﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using static RetroPass.PlaylistLaunchBox;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RetroPass
{
	[Serializable, XmlRoot("dataSource")]
	public class RetroPassConfig
	{
		public enum DataSourceType
		{
			LaunchBox,
			EmulationStation,
		}

		public class Retroarch
		{
			public string romPath;
			[XmlArrayItemAttribute("dir", IsNullable = false)]
			public RetroarchDirMap[] dirMappings;
		}

		[SerializableAttribute()]
		public partial class RetroarchDirMap
		{
			[XmlAttributeAttribute()]
			public string src;
			[XmlAttributeAttribute()]
			public string dst;
		}

		public string name;
		public bool log;
		public DataSourceType type;
		public string relativePath;
		public Retroarch retroarch;
	}

	[Serializable, XmlRoot("retropass")]
	public class RetroPassConfig_V1_6
	{	
		[XmlArrayItem("dataSource", typeof(RetroPassConfig))]
		public List<RetroPassConfig> dataSources;
	}

	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page
	{
		List<DataSource> loadedActiveDataSources = new List<DataSource>();
		PlaylistPlayLater playlistPlayLater = new PlaylistPlayLater();
		ListView currentListView;
		Button currentButton;
		DataSourceManager dataSourceManager;
		StackPanel stackPanelPlayLater;
		bool activeDataSourcesChanged = false;

		protected async override void OnKeyDown(KeyRoutedEventArgs e)
		{
			switch (e.Key)
			{
				case VirtualKey.GamepadX:
				case VirtualKey.X:
					// Gamepad X button was pressed
					if (currentListView != null && currentListView.SelectedItem != null)
					{
						int lastIndex = currentListView.SelectedIndex;
						PlaylistItem pl = currentListView.SelectedItem as PlaylistItem;
						playlistPlayLater.UpdatePlaylistPlayLater(pl);
						PlayLaterControl.UpdatePlayLaterControl(pl, playlistPlayLater);
						//if this is item from playlistPlayLater, then currentListView is playLater list view
						//so set proper selected item in case of deletion
						if (pl.playlist == playlistPlayLater)
						{
							currentListView.SelectedIndex = Math.Min(lastIndex, playlistPlayLater.PlaylistItemsLandingPage.Count - 1);
						}
						if (playlistPlayLater.PlaylistItems.Count == 0)
						{
							stackPanelPlayLater.Visibility = Visibility.Collapsed;
						}
						else
						{
							stackPanelPlayLater.Visibility = Visibility.Visible;
						}
					}
					break;

				case VirtualKey.GamepadY:
				case VirtualKey.Y:
					await ShowSearch();
					break;
				case VirtualKey.GamepadMenu:
					if (currentListView != null && currentListView.SelectedItem != null)
					{
						int lastIndex = currentListView.SelectedIndex;
						PlaylistItem pl = currentListView.SelectedItem as PlaylistItem;
						GameDetailsPage.StartContent(pl);
					}
					break;
			}
			base.OnKeyDown(e);
		}

		public MainPage()
		{
			this.InitializeComponent();
			this.Loaded += MainPage_Loaded;
			dataSourceManager = new DataSourceManager();
			dataSourceManager.ActiveDataSourcesChanged += ActiveDataSourceChanged;
			LogPage.SetLogging();
		}

		private void ActiveDataSourceChanged()
		{
			activeDataSourcesChanged = true;
		}

		private void ClearAll()
		{
			//clear all 
			foreach (var dataSource in loadedActiveDataSources)
			{
				dataSource.PlatformImported -= OnPlatformImported;
				dataSource.PlaylistImported -= OnPlaylistImported;
				dataSource.Platforms.Clear();
				dataSource.Playlists.Clear();
			}

			ClearMainPanel();
			loadedActiveDataSources.Clear();
			playlistPlayLater = new PlaylistPlayLater();
		}

		private async void MainPage_Loaded(object sender, RoutedEventArgs e)
		{
			//SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
			//			   AppViewBackButtonVisibility.Collapsed;
			SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;

			var dataSourceItems = await dataSourceManager.GetPresentActiveDataSources();

			if (dataSourceItems.Count == 0)
			{
				if (activeDataSourcesChanged)
				{
					activeDataSourcesChanged = false;
					ClearAll();
				}

				Frame.Navigate(typeof(SettingsPage), dataSourceManager);
				return;
			}

			//load data source only the first time or when it's changed
			if (activeDataSourcesChanged)
			{
				activeDataSourcesChanged = false;

				ClearAll();
				
				await playlistPlayLater.Load(dataSourceItems);
				playlistPlayLater.UpdateGamesLandingPage();
				OnPlaylistImported(playlistPlayLater);

				foreach (var dataSource in dataSourceItems)
				{
					loadedActiveDataSources.Add(dataSource);
					dataSource.PlatformImported += OnPlatformImported;
					dataSource.PlaylistImported += OnPlaylistImported;
					await dataSource.Load();
				}
			}			

			//focus on button only if getting from another page
			//this is to prevent setting focus after delayed load while search page is visible
			if (currentButton != null && SearchPage.Instance != null && SearchPage.Instance.IsOpened == false)
			{
				currentButton.Focus(FocusState.Programmatic);
			}

			if (playlistPlayLater.PlaylistItems.Count == 0)
			{
				stackPanelPlayLater.Visibility = Visibility.Collapsed;
			}
			else
			{
				stackPanelPlayLater.Visibility = Visibility.Visible;
			}
		}

		public static bool IsBackOrEscapeKey(Windows.System.VirtualKey key)
		{
			return key == VirtualKey.Back || key == VirtualKey.Escape || key == VirtualKey.GamepadB;
		}

		private void OnBackRequested(object sender, BackRequestedEventArgs e)
		{
			//int i = 5;
			//e.Handled = true;
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{

			base.OnNavigatedTo(e);
		}

		protected override void OnNavigatedFrom(NavigationEventArgs e)
		{
			SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
			base.OnNavigatedFrom(e);
		}

		private void ClearMainPanel()
		{
			var stackPanelPlaylists = StackPanelMain.Children.Where(t => t is StackPanel && ((StackPanel)t).Name != "StackPanelMenu").ToList();

			for (int i = stackPanelPlaylists.Count() - 1; i >= 0; i--)
			{
				StackPanelMain.Children.Remove(stackPanelPlaylists[i]);
			}

			stackPanelPlayLater = null;
		}

		private void AddList(Playlist playlist, bool prepend)
		{
			ListView listView = new ListView();

			listView.ItemTemplate = (DataTemplate)Resources["PlaylistItemTemplate"];
			listView.ItemsSource = playlist.PlaylistItemsLandingPage;
			listView.ItemsPanel = (ItemsPanelTemplate)Resources["GamesListViewPanelTemplate"];

			ListView lvTemplate = (ListView)Resources["ListViewPlatformTemplate"];
			listView.ItemContainerStyle = lvTemplate.ItemContainerStyle;

			listView.ContainerContentChanging += GamesListView_ContainerContentChanging;
			listView.SelectionMode = ListViewSelectionMode.Single;
			listView.SingleSelectionFollowsFocus = true;
			listView.GotFocus += ListView_GotFocus;
			listView.IsItemClickEnabled = true;
			listView.ItemClick += OnItemClick;
			listView.SelectedIndex = 0;

			StackPanel stackPanel = new StackPanel();
			Button button = new Button();
			button.DataContext = playlist;
			button.BringIntoViewRequested += Grid_BringIntoViewRequested;
			button.Style = (Style)Resources["PlatformFirstItemButtonStyle"];
			button.Click += Button_Click;
			button.GotFocus += Button_GotFocus;

			stackPanel.Orientation = Orientation.Horizontal;
			stackPanel.Children.Add(button);
			stackPanel.Children.Add(listView);

			//remember playlater stack panel so it can be hidden when there is nothing in the list
			if (playlist is PlaylistPlayLater)
			{
				stackPanelPlayLater = stackPanel;
				//stackPanelPlayLater.Visibility = Visibility.Collapsed;

				if (playlist.PlaylistItems.Count == 0)
				{
					stackPanelPlayLater.Visibility = Visibility.Collapsed;
				}
				else
				{
					stackPanelPlayLater.Visibility = Visibility.Visible;
				}
			}

			if (prepend)
			{
				StackPanelMain.Children.Insert(0, stackPanel);
			}
			else
			{
				StackPanelMain.Children.Add(stackPanel);
			}
		}

		private void Button_GotFocus(object sender, RoutedEventArgs e)
		{
			PlayLaterControl.UpdatePlayLaterControl(null, null);
			currentButton = sender as Button;
			currentListView = null;
		}

		private void ListView_GotFocus(object sender, RoutedEventArgs e)
		{
			ListView lv = sender as ListView;
			currentButton = null;

			if (currentListView != lv)
			{
				if (currentListView == null)
				{
					lv.SelectedIndex = 0;
				}
				else if (currentListView != null && currentListView.SelectedIndex != -1)
				{
					lv.SelectedIndex = Math.Min(currentListView.SelectedIndex, lv.Items.Count - 1);
					//if(playlistItem.game.)
				}
				currentListView = lv;
			}

			PlaylistItem playlistItem = lv.SelectedItem as PlaylistItem;
			PlayLaterControl.UpdatePlayLaterControl(playlistItem, playlistPlayLater);
		}

		private void OnPlaylistImported(Playlist playlist)
		{
			AddList(playlist, false);
		}

		private void OnPlatformImported(Platform platform)
		{
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			//remember currently focused element
			FocusManager.GetFocusedElement();

			Button b = sender as Button;
			//currentButton = b;
			Playlist p = b.DataContext as Playlist;
			this.Frame.Navigate(typeof(GameCollectionPage), new GameCollectionPage.GameCollectionPageNavigateParams(null, p, playlistPlayLater));
		}

		private void ScrollToCenter(UIElement sender, BringIntoViewRequestedEventArgs args)
		{
			if (args.VerticalAlignmentRatio != 0.5)  // Guard against our own request
			{
				args.Handled = true;
				// Swallow this request and restart it with a request to center the item.  We could instead have chosen
				// to adjust the TargetRect’s Y and Height values to add a specific amount of padding as it bubbles up, 
				// but if we just want to center it then this is easier.

				// (Optional) Account for sticky headers if they exist
				var headerOffset = 0.0;

				// Issue a new request
				args.TargetElement.StartBringIntoView(new BringIntoViewOptions()
				{
					AnimationDesired = true,
					VerticalAlignmentRatio = 0.5, // a normalized alignment position (0 for the top, 1 for the bottom)
					VerticalOffset = headerOffset, // applied after meeting the alignment ratio request
				});
			}
		}

		private void Grid_BringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args)
		{
			ScrollToCenter(sender, args);
		}

		// The BringIntoViewRequested event is raised by the framework when items receive keyboard (or Narrator) focus or 
		// someone triggers it with a call to UIElement.StartBringIntoView.

		private void PlatformItemsWrapGrid_BringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args)
		{
			//currentButton = args.OriginalSource as FrameworkElement;
			ScrollToCenter(sender, args);
		}

		private async void OnItemClick(object sender, ItemClickEventArgs e)
		{
			var item = e.ClickedItem as PlaylistItem;
			GameDetailsPage popup = new GameDetailsPage();
			popup.OnNavigatedTo(item);
			await popup.ShowAsync();
			popup.OnNavigatedFrom();
		}

		private void GamesListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
		{
			if (args.InRecycleQueue)
			{
				var templateRoot = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
				if (templateRoot != null)
				{
					var image = (Image)templateRoot.FindName("ItemImage");

					image.Source = null;
				}
				args.Handled = true;
			}

			if (args.Phase == 0)
			{
				args.RegisterUpdateCallback(ShowImage);
				args.Handled = true;
			}
		}

		private async Task ShowSearch()
		{
			//search only platform playlists and not custom playlists
			//List<Playlist> searchPlaylists = dataSource.Playlists.FindAll(t => dataSource.Platforms.Exists(p => p.Name == t.Name));

			/*List<Playlist> searchPlaylists = new List<Playlist>();

			foreach (DataSource dataSource in loadedDataSources)
			{
				searchPlaylists.AddRange(dataSource.Playlists.FindAll(t => dataSource.Platforms.Any(p => p.Name == t.Name)));
			}*/

			List<Playlist> searchPlaylists = loadedActiveDataSources.SelectMany(ds => ds.Playlists.Where(pl => ds.Platforms.Any(p => p.Name == pl.Name))).ToList();
			SearchPage.Instance.OnNavigatedTo(searchPlaylists);
			await SearchPage.Instance.ShowAsync();
			SearchPage.Instance.OnNavigatedFrom();

			if (SearchPage.Instance.selectedPlaylistItem != null)
			{
				GameDetailsPage popupDetails = new GameDetailsPage();
				popupDetails.OnNavigatedTo(SearchPage.Instance.selectedPlaylistItem);
				await popupDetails.ShowAsync();
				popupDetails.OnNavigatedFrom();
			}
		}

		private async void ShowImage(ListViewBase sender, ContainerContentChangingEventArgs args)
		{
			if (args.Phase == 1)
			{
				// It's phase 1, so show this item's image.
				var templateRoot = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
				var image = (Image)templateRoot.FindName("ItemImage");
				image.Opacity = 100;

				var item = args.Item as PlaylistItem;

				image.Source = await item.game.GetImageThumbnailAsync();

				if (image.Source == null)
				{
					BitmapImage bitmapImage = new BitmapImage();
					bitmapImage.UriSource = new Uri(image.BaseUri, "Assets/empty.png");
					image.Source = bitmapImage;
				}
			}
		}

		private void Settings_Click(object sender, RoutedEventArgs e)
		{
			Frame.Navigate(typeof(SettingsPage), dataSourceManager);
		}

		private async void Search_Click(object sender, RoutedEventArgs e)
		{
			await ShowSearch();
		}
	}
}
