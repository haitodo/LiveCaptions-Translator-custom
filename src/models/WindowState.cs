using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiveCaptionsTranslator.Utils;

namespace LiveCaptionsTranslator.models
{
    public class MainWindowState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool topmost = true;
        private bool captionLogEnabled = false;
        private bool latencyShow = false;
        private bool showOriginalCaption = false;
        private bool autoScrollEnabled = true;

        private int captionFontSize = 18;
        private Color captionFontColor = Color.Default;
        private bool captionFontBold = false;
        private string captionFontFamily = "Default";
        private double opacity = 1.0;
        private int lineSpacingOriginalTranslated = 3;
        private double lineHeightMultiplier = 1.25;
        private int itemSpacing = 6;

        public bool Topmost
        {
            get => topmost;
            set
            {
                topmost = value;
                OnPropertyChanged("Topmost");
            }
        }
        public bool CaptionLogEnabled
        {
            get => captionLogEnabled;
            set
            {
                captionLogEnabled = value;
                OnPropertyChanged("CaptionLogEnabled");
            }
        }
        public bool ShowOriginalCaption
        {
            get => showOriginalCaption;
            set
            {
                showOriginalCaption = value;
                OnPropertyChanged("ShowOriginalCaption");
            }
        }
        public bool AutoScrollEnabled
        {
            get => autoScrollEnabled;
            set
            {
                autoScrollEnabled = value;
                OnPropertyChanged("AutoScrollEnabled");
            }
        }
        public bool LatencyShow
        {
            get => latencyShow;
            set
            {
                latencyShow = value;
                OnPropertyChanged("LatencyShow");
            }
        }
        public int CaptionFontSize
        {
            get => captionFontSize;
            set
            {
                captionFontSize = value;
                OnPropertyChanged("CaptionFontSize");
                OnPropertyChanged("CaptionFontSizeOriginal");
                OnPropertyChanged("CaptionFontSizeLog");
                OnPropertyChanged("CaptionFontSizeLogOriginal");
            }
        }
        public Color CaptionFontColor
        {
            get => captionFontColor;
            set
            {
                captionFontColor = value;
                OnPropertyChanged("CaptionFontColor");
            }
        }
        public bool CaptionFontBold
        {
            get => captionFontBold;
            set
            {
                captionFontBold = value;
                OnPropertyChanged("CaptionFontBold");
            }
        }
        public string CaptionFontFamily
        {
            get => captionFontFamily;
            set
            {
                captionFontFamily = value;
                OnPropertyChanged("CaptionFontFamily");
            }
        }
        public double Opacity
        {
            get => opacity;
            set
            {
                opacity = value;
                OnPropertyChanged("Opacity");
            }
        }

        public int LineSpacingOriginalTranslated
        {
            get => lineSpacingOriginalTranslated;
            set
            {
                lineSpacingOriginalTranslated = value;
                OnPropertyChanged("LineSpacingOriginalTranslated");
            }
        }

        public double LineHeightMultiplier
        {
            get => lineHeightMultiplier;
            set
            {
                lineHeightMultiplier = value;
                OnPropertyChanged("LineHeightMultiplier");
            }
        }

        public int ItemSpacing
        {
            get => itemSpacing;
            set
            {
                itemSpacing = value;
                OnPropertyChanged("ItemSpacing");
            }
        }



        [System.Text.Json.Serialization.JsonIgnore]
        public int CaptionFontSizeOriginal => Math.Max(8, captionFontSize - 3);

        [System.Text.Json.Serialization.JsonIgnore]
        public int CaptionFontSizeLog => Math.Max(8, (int)(captionFontSize * 0.86));

        [System.Text.Json.Serialization.JsonIgnore]
        public int CaptionFontSizeLogOriginal => Math.Max(8, (int)(captionFontSize * 0.7));

        public void OnPropertyChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            Translator.Setting?.Save();
        }
    }

    public class OverlayWindowState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int fontSize = 15;
        private Color fontColor = Color.White;
        private FontBold fontBold = FontBold.None;
        private double fontStroke = 0.0;

        private Color backgroundColor = Color.Black;
        private int opacity = 150;

        public int FontSize
        {
            get => fontSize;
            set
            {
                fontSize = value;
                OnPropertyChanged("FontSize");
            }
        }
        public Color FontColor
        {
            get => fontColor;
            set
            {
                fontColor = value;
                OnPropertyChanged("FontColor");
            }
        }
        public FontBold FontBold
        {
            get => fontBold;
            set
            {
                fontBold = value;
                OnPropertyChanged("FontBold");
            }
        }
        public double FontStroke
        {
            get => fontStroke;
            set
            {
                fontStroke = value;
                OnPropertyChanged("FontStroke");
            }
        }
        public Color BackgroundColor
        {
            get => backgroundColor;
            set
            {
                backgroundColor = value;
                OnPropertyChanged("BackgroundColor");
            }
        }
        public int Opacity
        {
            get => opacity;
            set
            {
                opacity = value;
                OnPropertyChanged("Opacity");
            }
        }

        public void OnPropertyChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            Translator.Setting?.Save();
        }
    }
}