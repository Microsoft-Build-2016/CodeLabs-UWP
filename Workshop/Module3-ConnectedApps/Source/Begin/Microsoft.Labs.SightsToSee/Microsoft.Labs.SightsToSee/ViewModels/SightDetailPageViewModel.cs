﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Labs.SightsToSee.Library.Models;
using Microsoft.Labs.SightsToSee.Library.Services.DataModelService;
using Microsoft.Labs.SightsToSee.Mvvm;
using Microsoft.Labs.SightsToSee.Services.RestaurantDataService;
using Microsoft.Labs.SightsToSee.Services.TilesNotificationsService;
using Microsoft.Labs.SightsToSee.Services.FileService;
using Microsoft.Labs.SightsToSee.Views;

namespace Microsoft.Labs.SightsToSee.ViewModels
{
    public class SightDetailPageViewModel : ViewModelBase
    {
        private const string SightFilesPath = "SightFiles";
        private readonly IDataModelService _dataModelService = DataModelServiceFactory.CurrentDataModelService();
        private Sight _currentSight;
        private DateTimeOffset? _currentSightDate;
        private ObservableCollection<SightFile> _currentSightFiles;
        private TimeSpan _currentSightTime;
        private bool _isNotesInking;
        private bool _isImageInking;
        private SightFile _selectedSightFile;
        private BitmapImage _sightImage;
        private EatsControlViewModel _eatsControlViewModel;

        public bool IsNotesInking
        {
            get { return _isNotesInking; }
            set
            {
                if (_isNotesInking == value) return;
                Set(ref _isNotesInking, value);
            }
        }

        public bool IsImageInking
        {
            get { return _isImageInking; }
            set
            {
                if (_isImageInking == value) return;
                Set(ref _isImageInking, value);
            }
        }

        public DateTimeOffset? CurrentSightDate
        {
            get { return _currentSightDate ?? (_currentSightDate = DateTime.Now); }
            set { Set(ref _currentSightDate, value); }
        }

        public TimeSpan CurrentSightTime
        {
            get { return _currentSightTime; }
            set { Set(ref _currentSightTime, value); }
        }

        public Sight CurrentSight
        {
            get { return _currentSight; }
            set { Set(ref _currentSight, value); }
        }

        public ObservableCollection<SightFile> CurrentSightFiles
        {
            get { return _currentSightFiles; }

            set
            {
                if (value == _currentSightFiles)
                    return;
                Set(ref _currentSightFiles, value);
            }
        }

        public SightFile SelectedSightFile
        {
            get { return _selectedSightFile; }
            set
            {
                if (value == _selectedSightFile) return;
                Set(ref _selectedSightFile, value);
            }
        }

        public BitmapImage SightImage
        {
            get { return _sightImage; }
            set { Set(ref _sightImage, value); }
        }

        public EatsControlViewModel EatsControlViewModel
        {
            get { return _eatsControlViewModel; }
            set { Set(ref _eatsControlViewModel, value); }
        }


        public async Task LoadSightAsync(Guid sightId)
        {
            CurrentSight = await _dataModelService.LoadSightAsync(sightId);
            CurrentSightFiles = new ObservableCollection<SightFile>();
            SightImage = CurrentSight.ImageUri;
            foreach (var sightFile in CurrentSight.SightFiles)
            {
                // Only add Image files to the CurrentSightFiles list, not inking
                if (sightFile.FileType == SightFileType.ImageGallery)
                {
                    CurrentSightFiles.Add(sightFile);
                }
            }

            SelectedSightFile = CurrentSightFiles.FirstOrDefault();

            EatsControlViewModel = new EatsControlViewModel { CenterLocation = CurrentSight.Location, Sight = CurrentSight, IsDisplayingSightEats = true};
            try
            {
                EatsControlViewModel.IsLoadingEats = true;
                EatsControlViewModel.Eats =
                    new ObservableCollection<Restaurant>(
                        await RestaurantDataService.Current.GetRestaurantsForSightAsync(CurrentSight));
            }
            finally
            {
                EatsControlViewModel.IsLoadingEats = false;
            }
        }

        public async void DeleteSightAsync()
        {
            CurrentSight.IsMySight = false;
            await UpdateSightAsync(CurrentSight);
        }

        public async Task UpdateSightAsync(Sight sight)
        {
            await _dataModelService.SaveSightAsync(sight);
            await LoadSightAsync(sight.Id);
        }

        // Insert the M3_DragOver snippet here


        // Insert the M3_DropAsync snippet here


        public async Task SaveSightFileAsync(SightFile sightFile)
        {

            CurrentSight.SightFiles.Add(sightFile);
            CurrentSightFiles.Add(sightFile);
            await _dataModelService.SaveSightFileAsync(sightFile);
        }

        public void GalleryItemClicked(object sender, SelectionChangedEventArgs e)
        {
            var view = sender as GridView;
            if (view != null)
            {
                SelectedSightFile = view.SelectedItem as SightFile;
            }

            var listView = sender as ListView;
            if (listView != null)
            {
                SelectedSightFile = listView.SelectedItem as SightFile;
            }

            if (SelectedSightFile == null) return;

            SightImage = SelectedSightFile.ImageUri;
        }

        public async void AddSightAsync()
        {
            // Add CurrentSightTime and schedule toast notification
            if (CurrentSightDate.HasValue)
                CurrentSight.VisitDate = CurrentSightDate.Value.Date.Add(CurrentSightTime);

            ScheduledNotificationService.AddToastReminder(CurrentSight);

            CurrentSight.IsMySight = true;
            await UpdateSightAsync(CurrentSight);
        }

        // Insert the M3_ShareSight snippet here
        

        //Insert the M3_DataRequested snippet here


        //Insert the M3_GetDirections snippet here


        public async Task<SightFile> CreateSightFileAndAssociatedStorageFileAsync()
        {
            // Id of the new SightFile
            Guid sightFileId = Guid.NewGuid();
            StorageFolder sightFolder = await GetStorageFolderForSightFile(sightFileId);

            // Create the physical file
            var attachedFile = await
                    sightFolder.CreateFileAsync($"{Guid.NewGuid().ToString("D")}.png",
                        CreationCollisionOption.GenerateUniqueName);

            var sightFile = new SightFile
            {
                Id = sightFileId,
                FileType = SightFileType.General,
                Sight = CurrentSight,
                SightId = CurrentSight.Id,
                FileName = attachedFile.Name,
                Uri = attachedFile.GetUri().ToString()
            };

            return sightFile;
        }

        public async Task<StorageFolder> GetStorageFolderForSightFile(Guid sightFileId)
        {
            // Physical file needs to go to {localfolder}/SightFiles/{sightFileId}/
            var sightFilesFolder = await
                    ApplicationData.Current.LocalFolder.CreateFolderAsync(SightFilesPath,
                        CreationCollisionOption.OpenIfExists);
            var sightFolder = await sightFilesFolder.CreateFolderAsync(sightFileId.ToString("D"),
                CreationCollisionOption.OpenIfExists);
            return sightFolder;
        }

        public async Task UpdateSightFileImageUriAsync(Uri imageUri)
        {
            // Ensure that the original Uri is set, so that the user can revert later on
            SelectedSightFile.OriginalUri = 
                (SelectedSightFile.OriginalUri != null ? SelectedSightFile.OriginalUri : SelectedSightFile.Uri);

            // Update details in the SightFile object
            SelectedSightFile.Uri = imageUri.ToString();
            var file = await StorageFile.GetFileFromApplicationUriAsync(imageUri);
            SelectedSightFile.FileName = file.Name;

            // Update in the data model
            await UpdateSelectedSightFileAsync();
        }

        public async Task RevertSightFileToOriginalUriAsync()
        {
            SelectedSightFile.Uri = SelectedSightFile.OriginalUri;
            await UpdateSelectedSightFileAsync();
        }

        public async Task UpdateSelectedSightFileAsync()
        {
            await _dataModelService.UpdateSightFileAsync(SelectedSightFile);
            SightImage = SelectedSightFile.ImageUri;
        }

        public async Task ReplaceSelectedSightFileAsync(SightFile replacement)
        {
            // Delete in the database
            await _dataModelService.DeleteSightFileAsync(SelectedSightFile);
            // And save the replacement
            await _dataModelService.SaveSightFileAsync(replacement);


            // Replace the current version with the updated one
            int position = CurrentSightFiles.IndexOf(SelectedSightFile);
            CurrentSightFiles.Insert(position, replacement);
            CurrentSightFiles.RemoveAt(position + 1);
            SightImage = replacement.ImageUri;
        }

        public void EnableImageInk()
        {
            IsImageInking = true;
        }

        public void DisableImageInk()
        {
            IsImageInking = false;
        }

        public void EnableInk()
        {
            IsNotesInking = true;
        }
    }
}