﻿using BrickController2.CreationManagement;
using BrickController2.DeviceManagement;
using BrickController2.HardwareServices.GameController;
using BrickController2.UI.Commands;
using BrickController2.UI.Services.Dialog;
using BrickController2.UI.Services.Navigation;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BrickController2.UI.ViewModels
{
    public class PlayerPageViewModel : PageViewModelBase
    {
        private readonly ICreationManager _creationManager;
        private readonly IDeviceManager _deviceManager;
        private readonly IDialogService _dialogService;
        private readonly IGameControllerService _gameControllerService;

        private readonly IList<Device> _devices = new List<Device>();
        private readonly IList<Device> _buwizzDevices = new List<Device>();
        private readonly IList<Device> _buwizz2Devices = new List<Device>();

        private ControllerProfile _activeProfile;

        public PlayerPageViewModel(
            INavigationService navigationService,
            ICreationManager creationManager,
            IDeviceManager deviceManager,
            IDialogService dialogService,
            IGameControllerService gameControllerService,
            NavigationParameters parameters
            )
            : base(navigationService)
        {
            _creationManager = creationManager;
            _deviceManager = deviceManager;
            _dialogService = dialogService;
            _gameControllerService = gameControllerService;

            Creation = parameters.Get<Creation>("creation");
            CollectDevices();
            _activeProfile = Creation.ControllerProfiles.First();

            ControllerProfileTappedCommand = new SafeCommand<ControllerProfile>(profile => ControllerProfileTapped(profile));
            BuWizzOutputLevelChangedCommand = new SafeCommand<int>(level => ChangeOutputLevel(level, _buwizzDevices));
            BuWizz2OutputLevelChangedCommand = new SafeCommand<int>(level => ChangeOutputLevel(level, _buwizz2Devices));
        }

        public Creation Creation { get; }

        public bool HasBuWizzDevice => _buwizzDevices.Count > 0;
        public bool HasBuWizz2Device => _buwizz2Devices.Count > 0;

        public ICommand ControllerProfileTappedCommand { get; }
        public ICommand BuWizzOutputLevelChangedCommand { get; }
        public ICommand BuWizz2OutputLevelChangedCommand { get; }

        public override async void OnAppearing()
        {
            base.OnAppearing();

            _gameControllerService.GameControllerEvent += GameControllerEventHandler;
            await ConnectDevicesAsync();
        }

        public override async void OnDisappearing()
        {
            _gameControllerService.GameControllerEvent -= GameControllerEventHandler;
            await DisconnectDevicesAsync();

            base.OnDisappearing();
        }

        private void CollectDevices()
        {
            foreach (var profile in Creation.ControllerProfiles)
            {
                foreach (var controllerEvent in profile.ControllerEvents)
                {
                    foreach (var controllerAction in controllerEvent.ControllerActions)
                    {
                        var deviceId = controllerAction.DeviceId;
                        var device = _deviceManager.GetDeviceById(deviceId);
                        if (device != null && !_devices.Contains(device))
                        {
                            _devices.Add(device);

                            if (device.DeviceType == DeviceType.BuWizz)
                            {
                                _buwizzDevices.Add(device);
                            }

                            if (device.DeviceType == DeviceType.BuWizz2)
                            {
                                _buwizz2Devices.Add(device);
                            }
                        }
                    }
                }
            }
        }

        private async Task ConnectDevicesAsync()
        {
            await _dialogService.ShowProgressDialogAsync(
                false,
                async (progressDialog, token) =>
                {
                    var connectTasks = new List<Task>();

                    foreach (var device in _devices)
                    {
                        connectTasks.Add(device.ConnectAsync(token));
                    }

                    await Task.WhenAll(connectTasks);
                },
                "Connecting...",
                null,
                "Cancel");

            if (_devices.Any(d => d.DeviceState != DeviceState.Connected))
            {
                await DisconnectDevicesAsync();
                await NavigationService.NavigateBackAsync();
            }
        }

        private async Task DisconnectDevicesAsync()
        {
            await _dialogService.ShowProgressDialogAsync(
                false,
                async (progressDialog, token) =>
                {
                    var tasks = new List<Task>();

                    foreach (var device in _devices)
                    {
                        tasks.Add(device.DisconnectAsync());
                    }

                    await Task.WhenAll(tasks);
                },
                "Disconnecting...",
                null,
                "Cancel");
        }

        private void ControllerProfileTapped(ControllerProfile profile)
        {
            _activeProfile = profile;
        }

        private void ChangeOutputLevel(int level, IList<Device> devices)
        {
            foreach (var device in devices)
            {
                device.SetOutputLevel(level);
            }
        }

        private void GameControllerEventHandler(object sender, GameControllerEventArgs e)
        {
            // TODO: implement
        }
    }
}