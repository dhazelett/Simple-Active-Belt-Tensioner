using Microsoft.VisualBasic;
using SimHub;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace User.ActiveBeltTensioner
{
    public class DeviceSettings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private const string _deviceNotFound = "N/A";

        private readonly object _profilesLock = new object();

        private bool _isInitialised = false;

        public void Initialise()
        {
            _isInitialised = true;
            ChangeActiveProfile();
        }

        private void InvokePropertyChange([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            GameTuningProfile profile = FindProfile(this.CurrentGame, this.CurrentVehicle, true);

            if (profile is GameTuningProfile)
            {
                profile.SetTuningProperty(
                    name,
                    this.GetType().GetProperty(name)?.GetValue(this)
                );
            }
        }

        private string _serialPort = _deviceNotFound;
        public string SerialPort
        {
            get { return _serialPort; }
            set
            {
                if (_serialPort != value)
                {
                    _serialPort = value ?? _deviceNotFound;
                    InvokePropertyChange(nameof(SerialPort));
                    InvokePropertyChange(nameof(IsSerialPortValid));
                }
            }
        }

        private bool _isEnabled = false;
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    InvokePropertyChange(nameof(IsEnabled));
                }
            }
        }

        private bool _startAutomatically = false;
        public bool StartAutomatically
        {
            get { return _startAutomatically; }
            set
            {
                if (_startAutomatically != value)
                {
                    _startAutomatically = value;
                    InvokePropertyChange(nameof(StartAutomatically));
                }
            }
        }

        private bool _isFlipped = false;
        public bool IsFlipped
        {
            get { return _isFlipped; }
            set
            {
                if (_isFlipped != value)
                {
                    _isFlipped = value;
                    InvokePropertyChange(nameof(IsFlipped));
                }
            }
        }

        private bool _isAutomaticallyTuning = false;
        public bool IsAutomaticallyTuning
        {
            get { return _isAutomaticallyTuning; }
            set
            {
                if (_isAutomaticallyTuning != value)
                {
                    _isAutomaticallyTuning = value;
                    InvokePropertyChange(nameof(IsAutomaticallyTuning));
                    InvokePropertyChange(nameof(IsNotAutomaticallyTuning));
                }
            }
        }
        public bool IsNotAutomaticallyTuning
        {
            get { return !_isAutomaticallyTuning; }
        }

        private int _idleTension = 150;
        public int IdleTension
        {
            get { return _idleTension; }
            set
            {
                if (_idleTension != value)
                {
                    _idleTension = value;
                    InvokePropertyChange(nameof(IdleTension));
                }
            }
        }

        private int _minimumTension = 200;
        public int MinimumTension
        {
            get { return _minimumTension; }
            set
            {
                if (_minimumTension != value)
                {
                    _minimumTension = Math.Min(value, _maximumTension);
                    InvokePropertyChange(nameof(MinimumTension));
                    InvokePropertyChange(nameof(IsMinimumTensionNonZero));
                }
            }
        }

        private int _maximumTension = 1000;
        public int MaximumTension
        {
            get { return _maximumTension; }
            set
            {
                if (_maximumTension != value)
                {
                    _maximumTension = Math.Max(value, _minimumTension);
                    InvokePropertyChange(nameof(MaximumTension));
                }
            }
        }

        private int _minimumSurge = -8;
        public int MinimumSurge
        {
            get { return _minimumSurge; }
            set
            {
                if (_minimumSurge != value)
                {
                    _minimumSurge = Math.Min(value, _maximumSurge);
                    InvokePropertyChange(nameof(MinimumSurge));
                }
            }
        }

        private int _maximumSurge = 25;
        public int MaximumSurge
        {
            get { return _maximumSurge; }
            set
            {
                if (_maximumSurge != value)
                {
                    _maximumSurge = Math.Max(value, _minimumSurge);
                    InvokePropertyChange(nameof(MaximumSurge));
                }
            }
        }

        private int _minimumSway = -25;
        public int MinimumSway
        {
            get { return _minimumSway; }
            set
            {
                if (_minimumSway != value)
                {
                    _minimumSway = Math.Min(value, _maximumSway);
                    InvokePropertyChange(nameof(MinimumSway));
                }
            }
        }

        private int _maximumSway = 25;
        public int MaximumSway
        {
            get { return _maximumSway; }
            set
            {
                if (_maximumSway != value)
                {
                    _maximumSway = Math.Max(value, _minimumSway);
                    InvokePropertyChange(nameof(MaximumSway));
                }
            }
        }

        private int _minimumHeave = -25;
        public int MinimumHeave
        {
            get { return _minimumHeave; }
            set
            {
                if (_minimumHeave != value)
                {
                    _minimumHeave = Math.Min(value, _maximumHeave);
                    InvokePropertyChange(nameof(MinimumHeave));
                }
            }
        }

        private int _maximumHeave = 90;
        public int MaximumHeave
        {
            get { return _maximumHeave; }
            set
            {
                if (_maximumHeave != value)
                {
                    _maximumHeave = Math.Max(value, _minimumHeave);
                    InvokePropertyChange(nameof(MaximumHeave));
                }
            }
        }

        private int _sideBias = 0;
        public int SideBias
        {
            get { return _sideBias; }
            set {
                if (_sideBias != value)
                {
                    _sideBias = value;
                    InvokePropertyChange(nameof(SideBias));
                }
            }
        }

        private int _smoothingFactor = 300;
        public int SmoothingFactor
        {
            get { return _smoothingFactor; }
            set
            {
                if (_smoothingFactor != value)
                {
                    _smoothingFactor = value;
                    InvokePropertyChange(nameof(SmoothingFactor));
                }
            }
        }

        private int _corneringStrength = 1000;
        public int CorneringStrength
        {
            get { return _corneringStrength; }
            set
            {
                if (_corneringStrength != value)
                {
                    _corneringStrength = value;
                    InvokePropertyChange(nameof(CorneringStrength));
                }
            }
        }

        private int _accelerationStrength = 1000;
        public int AccelerationStrength
        {
            get { return _accelerationStrength; }
            set
            {
                if (_accelerationStrength != value)
                {
                    _accelerationStrength = value;
                    InvokePropertyChange(nameof(AccelerationStrength));
                }
            }
        }

        private int _brakingStrength = 1000;
        public int BrakingStrength
        {
            get { return _brakingStrength; }
            set
            {
                if (_brakingStrength != value)
                {
                    _brakingStrength = value;
                    InvokePropertyChange(nameof(BrakingStrength));
                }
            }
        }

        private int _jumpingStrength = 1000;
        public int JumpingStrength
        {
            get { return _jumpingStrength; }
            set
            {
                if (_jumpingStrength != value)
                {
                    _jumpingStrength = value;
                    InvokePropertyChange(nameof(JumpingStrength));
                }
            }
        }

        private int _landingStrength = 1000;
        public int LandingStrength
        {
            get { return _landingStrength; }
            set
            {
                if (_landingStrength != value)
                {
                    _landingStrength = value;
                    InvokePropertyChange(nameof(LandingStrength));
                }
            }
        }

        private int _shiftingStrength = 0;
        public int ShiftingStrength
        {
            get { return _shiftingStrength; }
            set
            {
                if (_shiftingStrength != value)
                {
                    _shiftingStrength = value;
                    InvokePropertyChange(nameof(ShiftingStrength));
                }
            }
        }

        private string _currentGame = string.Empty;
        public string CurrentGame
        {
            get { return _currentGame; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_currentGame != newValue)
                {
                    _currentGame = newValue;
                    InvokePropertyChange(nameof(CurrentGame));
                    ChangeActiveProfile();
                }
            }
        }

        private string _currentVehicle = string.Empty;
        public string CurrentVehicle
        {
            get { return _currentVehicle; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_currentVehicle != newValue)
                {
                    _currentVehicle = newValue;
                    InvokePropertyChange(nameof(CurrentVehicle));
                    InvokePropertyChange(nameof(HasCurrentVehicle));
                    ChangeActiveProfile();
                }
            }
        }

        public bool HasCurrentVehicle
        {
            get { return _currentVehicle != string.Empty; }
        }

        public ObservableCollection<GameTuningProfile> Profiles { get; set; } = new ObservableCollection<GameTuningProfile>();

        public void AddProfile(GameTuningProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (profile.Game == String.Empty)
            {
                return;
            }

            lock (_profilesLock)
            {
                Profiles.Add(profile);
            }

            ChangeActiveProfile();
        }

        public void RemoveProfile(GameTuningProfile profile)
        {
            lock (_profilesLock)
            {
                Profiles.Remove(profile);
            }

            ChangeActiveProfile();
        }

        public GameTuningProfile CreateProfile(string game, string vehicle)
        {
            return GameTuningProfile.Make(this, game, vehicle);
        }

        public GameTuningProfile CloneProfile(GameTuningProfile profile, string game, string vehicle)
        {
            return profile == null ? null : profile.Clone(game, vehicle);
        }

        public void LoadProfile(GameTuningProfile profile)
        {
            MinimumSurge = profile.MinimumSurge;
            MaximumSurge = profile.MaximumSurge;
            MinimumSway = profile.MinimumSway;
            MaximumSway = profile.MaximumSway;
            MinimumHeave = profile.MinimumHeave;
            MaximumHeave = profile.MaximumHeave;
            SmoothingFactor = profile.SmoothingFactor;
        }

        public GameTuningProfile FindProfile(string game, string vehicle, bool useDefault = false)
        {
            lock (_profilesLock)
            {
                for (int i = Profiles.Count - 1; i >= 0; i--)
                {
                    if (Profiles[i].Matches(game, vehicle))
                    {
                        return Profiles[i];
                    }
                }

                if (useDefault && Profiles.Count > 0)
                {
                    for (int i = Profiles.Count - 1; i >= 0; i--)
                    {
                        if (Profiles[i].Matches(string.Empty, string.Empty))
                        {
                            return Profiles[i];
                        }
                    }
                }
            }

            return null;
        }

        public GameTuningProfile ChangeActiveProfile()
        {
            if (!_isInitialised)
            {
                return null;
            }

            Logging.Current.Info($"SABT: CHANGING ACTIVE PROFILE TO '{CurrentGame}' + '{CurrentVehicle}' ({Profiles.Count})");

            lock (_profilesLock)
            {
                for (int i = 0; i < Profiles.Count; i++)
                {
                    Profiles[i].IsActive = false;
                }

                CleanProfiles();

                GameTuningProfile profile = null;

                // Use Game & Vehicle Profile
                profile = FindProfile(CurrentGame, CurrentVehicle);

                if (profile is GameTuningProfile)
                {
                    profile.IsActive = true;

                    LoadProfile(profile);

                    return profile;
                }

                // Use Game Profile (Or Default)
                profile = FindProfile(CurrentGame, string.Empty, true);

                if (profile is GameTuningProfile)
                {
                    profile.IsActive = true;

                    LoadProfile(profile);

                    return profile;
                }

                Logging.Current.Info($"SABT: DEFAULT PROFILE MISSING '{Profiles.Count}'");

                // Create Default Profile
                profile = CreateProfile(string.Empty, string.Empty);
                profile.IsActive = true;

                Profiles.Insert(0, profile);

                Logging.Current.Info($"SABT: DEFAULT PROFILE INSERTED '{Profiles.Count}'");

                return profile;
            }
        }

        private void CleanProfiles()
        {
            // Identify & Remove Duplicate Profiles
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            for (int i = Profiles.Count - 1; i >= 0; i--)
            {
                if (!keys.Add(Profiles[i].GetKey()))
                {
                    Logging.Current.Info($"SABT: DUPLICATE PROFILE REMOVED AT {i}");

                    Profiles.RemoveAt(i);
                }
            }

            // Sort By Game Label, Then Vehicle Label
            var sorted = Profiles
                .OrderBy(p => !p.IsDefault)
                .ThenBy(p => p.GameLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.VehicleLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                int p = Profiles.IndexOf(sorted[i]);
                if (p != i)
                {
                    Profiles.Move(p, i);
                }
            }
        }

        public bool IsMinimumTensionNonZero
        {
            get { return MinimumTension > 0; }
        }

        public bool IsSerialPortValid
        {
            get { return !String.IsNullOrEmpty(_serialPort) && (_serialPort != _deviceNotFound); }
        }
    }




    public class GameTuningProfile : INotifyPropertyChanged
    {
        private const string _wildcardSymbol = "✱";

        public event PropertyChangedEventHandler PropertyChanged;

        private void InvokePropertyChange([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public bool IsDefault { get; }

        public bool IsNotDefault {
            get { return !IsDefault; }
        }
        
        public string Game { get; }
        public string GameLabel { get; set; }
        
        public string Vehicle { get; }
        public string VehicleLabel { get; set; }

        private bool _isActive;
        public bool IsActive
        {
            get { return _isActive; }
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    InvokePropertyChange(nameof(IsActive));
                }
            }
        }

        public int MinimumSurge { get; set; }
        public int MaximumSurge { get; set; }
        public int MinimumSway { get; set; }
        public int MaximumSway { get; set; }
        public int MinimumHeave { get; set; }
        public int MaximumHeave { get; set; }
        public int SmoothingFactor { get; set; }

        public GameTuningProfile(string game, string vehicle, bool promptForLabels = false)
        {
            Game = game;
            Vehicle = vehicle;
            IsDefault = (game == string.Empty && vehicle == string.Empty);

            if (promptForLabels)
            {
Logging.Current.Info($"SABT: PROMPTING FOR LABELS FOR '{game}' + '{vehicle}'");
                GameLabel = (game == string.Empty) ? _wildcardSymbol : Interaction.InputBox(
                    Prompt: "You may alter the game name from the one given by SimHub if desired:",
                    Title: "Set Game Name",
                    DefaultResponse: PrettifyLabelPart(game)
                );

                if (string.IsNullOrEmpty(GameLabel))
                {
                    GameLabel = PrettifyLabelPart(game);
                }

                VehicleLabel = (vehicle == string.Empty) ? _wildcardSymbol : Interaction.InputBox(
                    Prompt: "You may alter the vehicle name from the one given by SimHub if desired:",
                    Title: "Set Vehicle Name",
                    DefaultResponse: PrettifyLabelPart(vehicle)
                );

                if (string.IsNullOrEmpty(VehicleLabel))
                {
                    VehicleLabel = PrettifyLabelPart(vehicle);
                }
            }
            else {
Logging.Current.Info($"SABT: NOT PROMPTING FOR LABELS FOR '{game}' + '{vehicle}'");
            }
        }

        public static GameTuningProfile Make(DeviceSettings settings, string game, string vehicle)
        {
            Logging.Current.Info($"SABT: MAKE PROFILE '{game}' + '{vehicle}'");

            return new GameTuningProfile(game, vehicle, true)
            {
                IsActive = false,

                MinimumSurge = settings.MinimumSurge,
                MaximumSurge = settings.MaximumSurge,
                MinimumSway = settings.MinimumSway,
                MaximumSway = settings.MaximumSway,
                MinimumHeave = settings.MinimumHeave,
                MaximumHeave = settings.MaximumHeave,
                SmoothingFactor = settings.SmoothingFactor
            };
        }

        public GameTuningProfile Clone(string game, string vehicle)
        {
            Logging.Current.Info($"SABT: CLONE PROFILE TO '{game}' + '{vehicle}'");

            return new GameTuningProfile(game, vehicle, true)
            {
                IsActive = false,

                MinimumSurge = this.MinimumSurge,
                MaximumSurge = this.MaximumSurge,
                MinimumSway = this.MinimumSway,
                MaximumSway = this.MaximumSway,
                MinimumHeave = this.MinimumHeave,
                MaximumHeave = this.MaximumHeave,
                SmoothingFactor = this.SmoothingFactor
            };
        }

        public bool Matches(string game, string vehicle)
        {
            return (
                string.Equals(SimplifyKeyPart(Game), SimplifyKeyPart(game), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(SimplifyKeyPart(Vehicle), SimplifyKeyPart(vehicle), StringComparison.OrdinalIgnoreCase)
            );
        }

        public string GetKey()
        {
            return $"{SimplifyKeyPart(Game)}|{SimplifyKeyPart(Vehicle)}";
        }

        public void SetTuningProperty(string propertyName, object value)
        {
            var property = this.GetType().GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(this, value);
            }
        }

        private static string PrettifyLabelPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return _wildcardSymbol;
            }

            value = value.Trim();

            StringBuilder words = new StringBuilder(value.Length * 2);

            bool hasPreviousAlphaNumeric = false;
            bool previousWasLower = false;
            bool previousWasUpper = false;
            bool previousWasDigit = false;

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];

                if (!char.IsLetterOrDigit(current))
                {
                    if (words.Length > 0 && words[words.Length - 1] != ' ')
                    {
                        words.Append(' ');
                    }

                    hasPreviousAlphaNumeric = false;
                    previousWasLower = false;
                    previousWasUpper = false;
                    previousWasDigit = false;
                    continue;
                }

                bool currentIsLetter = char.IsLetter(current);
                bool currentIsLower = currentIsLetter && char.IsLower(current);
                bool currentIsUpper = currentIsLetter && char.IsUpper(current);
                bool currentIsDigit = char.IsDigit(current);

                if (hasPreviousAlphaNumeric)
                {
                    bool splitBeforeCurrent = (
                        (previousWasLower && currentIsUpper) ||
                        (previousWasDigit && !currentIsDigit) ||
                        (!previousWasDigit && currentIsDigit)
                    );

                    if (splitBeforeCurrent && words.Length > 0 && words[words.Length - 1] != ' ')
                    {
                        words.Append(' ');
                    }
                }

                words.Append(current);

                hasPreviousAlphaNumeric = true;
                previousWasLower = currentIsLower;
                previousWasUpper = currentIsUpper;
                previousWasDigit = currentIsDigit;
            }

            string separated = words.ToString().Trim();

            if (string.IsNullOrWhiteSpace(separated))
            {
                return "*";
            }

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                separated.ToLowerInvariant()
            );
        }

        private static string SimplifyKeyPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "*";
            }

            value = value.Trim();

            StringBuilder simplified = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];

                if (char.IsLetterOrDigit(current))
                {
                    simplified.Append(char.ToLowerInvariant(current));
                }
            }

            return simplified.ToString();
        }
    }
}
