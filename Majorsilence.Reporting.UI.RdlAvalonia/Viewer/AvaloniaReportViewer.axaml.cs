using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Majorsilence.Reporting.Rdl;
using Majorsilence.Reporting.RdlEngine;

namespace Majorsilence.Reporting.UI.RdlAvalonia.Viewer
{
    public partial class AvaloniaReportViewer : UserControl
    {
        private Report? _report;
        private Pages? _pages;
        private Uri? _sourceFile;
        private string? _sourceRdl;
        private IDictionary _parameters = new Dictionary<string, string>();
        private IList? _errorMessages;
        private int _pageCurrent = 1;
        private double _zoom = 1.0;
        private ZoomMode _zoomMode = ZoomMode.FitWidth;

        public AvaloniaReportViewer()
        {
            InitializeComponent();
            RdlEngineConfig.GetCustomReportTypes();
            InitializeUi();
        }

        public event EventHandler<SubreportDataRetrievalEventArgs>? SubreportDataRetrieval;

        public string? ConnectionStringOverride { get; private set; }

        public bool OverwriteSubreportConnection { get; private set; }

        public string? WorkingDirectory { get; set; }

        public async Task SetSourceFileAsync(Uri fileUri)
        {
            _sourceFile = fileUri;
            _sourceRdl = null;
            WorkingDirectory = Path.GetDirectoryName(fileUri.LocalPath);
            await RebuildAsync();
        }

        public async Task SetSourceRdlAsync(string rdl)
        {
            _sourceRdl = rdl;
            _sourceFile = null;
            await RebuildAsync();
        }

        public void SetReportParametersAmpersandSeparated(string parameterString)
        {
            _parameters = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(parameterString))
            {
                return;
            }

            string[] prms = parameterString.TrimEnd(';').Split('&');
            foreach (string p in prms)
            {
                int iEq = p.IndexOf("=", StringComparison.Ordinal);
                if (iEq > 0)
                {
                    string name = p.Substring(0, iEq);
                    string val = p.Substring(iEq + 1);
                    _parameters.Add(name, val);
                }
            }
        }

        public async Task RebuildAsync()
        {
            if (_sourceFile == null && string.IsNullOrWhiteSpace(_sourceRdl))
            {
                return;
            }

            _report = await GetReportAsync();
            if (_report == null)
            {
                return;
            }

            _pages = await BuildPagesAsync(_report);
            _pageCurrent = 1;

            ReportCanvas.SetReport(_report, _pages);
            UpdatePageUi();
            UpdateErrorsUi();
        }

        private void InitializeUi()
        {
            var zoomModes = new[] { ZoomMode.FitWidth, ZoomMode.FitPage, ZoomMode.ActualSize };
            ZoomModeComboBox.ItemsSource = zoomModes;
            ZoomModeComboBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ZoomMode>(
                (mode, _) => new Avalonia.Controls.TextBlock { Text = mode.ToDisplayString() });
            ZoomModeComboBox.SelectedItem = _zoomMode;
            UpdateStatusZoom();

            OpenButton.Click += OpenButtonOnClick;
            SaveButton.Click += SaveButtonOnClick;
            PrintButton.Click += PrintButtonOnClick;
            CopyButton.Click += (_, _) => ReportCanvas.CopySelection();
            SelectAllButton.Click += (_, _) => ReportCanvas.SelectAll();
            FirstPageButton.Click += (_, _) => SetPage(1);
            PreviousPageButton.Click += (_, _) => SetPage(_pageCurrent - 1);
            NextPageButton.Click += (_, _) => SetPage(_pageCurrent + 1);
            LastPageButton.Click += (_, _) => SetPage(_pages?.PageCount ?? 1);
            ZoomInButton.Click += (_, _) => SetZoom(_zoom + 0.25);
            ZoomOutButton.Click += (_, _) => SetZoom(Math.Max(0.25, _zoom - 0.25));
            ZoomModeComboBox.SelectionChanged += ZoomModeComboBoxOnSelectionChanged;
            PageTextBox.LostFocus += PageTextBoxOnLostFocus;
            ApplyParametersButton.Click += ApplyParametersButtonOnClick;
            ErrorsToggleButton.IsCheckedChanged += ErrorsToggleOnChanged;

            ReportScrollViewer.SizeChanged += (_, _) => ApplyZoomMode();
            ReportScrollViewer.AddHandler(PointerWheelChangedEvent, OnScrollViewerPointerWheelChanged, handledEventsToo: false);
        }

        private void OnScrollViewerPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl+Wheel = zoom in/out
                var delta = e.Delta.Y > 0 ? 0.1 : -0.1;
                SetZoom(Math.Max(0.1, _zoom + delta));
                e.Handled = true;
            }
        }

        private async void OpenButtonOnClick(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Open RDL Report",
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("RDL Files") { Patterns = new[] { "*.rdl" } },
                    new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });

            if (files.Count == 0)
            {
                return;
            }

            await SetSourceFileAsync(files[0].Path);
        }

        private async void SaveButtonOnClick(object? sender, RoutedEventArgs e)
        {
            if (_report == null)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save Report",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } },
                    new Avalonia.Platform.Storage.FilePickerFileType("HTML") { Patterns = new[] { "*.html", "*.htm" } },
                    new Avalonia.Platform.Storage.FilePickerFileType("XML") { Patterns = new[] { "*.xml" } },
                    new Avalonia.Platform.Storage.FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } },
                    new Avalonia.Platform.Storage.FilePickerFileType("MHTML") { Patterns = new[] { "*.mhtml", "*.mht" } },
                    new Avalonia.Platform.Storage.FilePickerFileType("RTF") { Patterns = new[] { "*.rtf" } },
                    new Avalonia.Platform.Storage.FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } }
                }
            });

            if (file == null)
            {
                return;
            }

            var filePath = file.Path.LocalPath;
            var outputType = OutputPresentationType.Internal;
            var ext = Path.GetExtension(filePath).Trim('.').ToLowerInvariant();
            switch (ext)
            {
                case "pdf":
                    outputType = OutputPresentationType.PDF;
                    break;
                case "xml":
                    outputType = OutputPresentationType.XML;
                    break;
                case "html":
                case "htm":
                    outputType = OutputPresentationType.HTML;
                    break;
                case "csv":
                    outputType = OutputPresentationType.CSV;
                    break;
                case "mht":
                case "mhtml":
                    outputType = OutputPresentationType.MHTML;
                    break;
                case "rtf":
                    outputType = OutputPresentationType.RTF;
                    break;
                case "xlsx":
                    outputType = OutputPresentationType.Excel2007;
                    break;
            }

            await SaveAsAsync(filePath, outputType);
        }

        private async void PrintButtonOnClick(object? sender, RoutedEventArgs e)
        {
            if (_report == null)
            {
                return;
            }

            // Avalonia does not expose a cross-platform print API yet; export to PDF for now.
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save as PDF for Printing",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } }
                }
            });

            if (file == null)
            {
                return;
            }

            var filePath = file.Path.LocalPath;
            await SaveAsAsync(filePath, OutputPresentationType.PDF);
        }

        private async Task SaveAsAsync(string filePath, OutputPresentationType outputType)
        {
            if (_report == null || _pages == null)
            {
                return;
            }

            OneFileStreamGen? sg = null;
            try
            {
                sg = new OneFileStreamGen(filePath, true);
                switch (outputType)
                {
                    case OutputPresentationType.PDF:
                        await _report.RunRender(sg, OutputPresentationType.PDF);
                        break;
                    case OutputPresentationType.CSV:
                        await _report.RunRender(sg, OutputPresentationType.CSV);
                        break;
                    case OutputPresentationType.RTF:
                        await _report.RunRender(sg, OutputPresentationType.RTF);
                        break;
                    case OutputPresentationType.Excel2007:
                        await _report.RunRender(sg, OutputPresentationType.Excel2007);
                        break;
                    case OutputPresentationType.XML:
                        await _report.RunRender(sg, OutputPresentationType.XML);
                        break;
                    case OutputPresentationType.HTML:
                        await _report.RunRender(sg, OutputPresentationType.HTML);
                        break;
                    case OutputPresentationType.MHTML:
                        await _report.RunRender(sg, OutputPresentationType.MHTML);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported export format: " + Path.GetExtension(filePath));
                }
            }
            finally
            {
                sg?.CloseMainStream();
            }
        }

        private async Task<Report?> GetReportAsync()
        {
            string source;
            if (!string.IsNullOrWhiteSpace(_sourceRdl))
            {
                source = _sourceRdl;
            }
            else if (_sourceFile != null)
            {
#if NET6_0_OR_GREATER
                source = await File.ReadAllTextAsync(_sourceFile.LocalPath);
#else
                source = File.ReadAllText(_sourceFile.LocalPath);
#endif
            }
            else
            {
                return null;
            }

            var parser = new RDLParser(source)
            {
                Folder = WorkingDirectory,
                OverwriteConnectionString = ConnectionStringOverride,
                OverwriteInSubreport = OverwriteSubreportConnection
            };

            var report = await parser.Parse();
            report.SubreportDataRetrieval += (_, args) => SubreportDataRetrieval?.Invoke(this, args);
            return report;
        }

        private async Task<Pages?> BuildPagesAsync(Report report)
        {
            try
            {
                await report.RunGetData(_parameters);
                var pages = await report.BuildPages();
                if (report.ErrorMaxSeverity > 0)
                {
                    _errorMessages = report.ErrorItems;
                    report.ErrorReset();
                }

                return pages;
            }
            catch
            {
                return null;
            }
        }

        private void UpdatePageUi()
        {
            if (_pages == null)
            {
                PageTextBox.Text = "0";
                PageCountTextBlock.Text = "/ 0";
                StatusPageTextBlock.Text = string.Empty;
                return;
            }

            PageTextBox.Text = _pageCurrent.ToString();
            PageCountTextBlock.Text = $"/ {_pages.PageCount}";
            StatusPageTextBlock.Text = $"Page {_pageCurrent} of {_pages.PageCount}";
            SetPage(_pageCurrent);
        }

        private void UpdateStatusZoom()
        {
            StatusZoomTextBlock.Text = $"{(int)Math.Round(_zoom * 100)} %";
        }

        private void UpdateErrorsUi()
        {
            if (_errorMessages == null)
            {
                ErrorsListBox.Items.Clear();
                return;
            }

            ErrorsListBox.Items.Clear();
            foreach (var message in _errorMessages)
            {
                ErrorsListBox.Items.Add(message);
            }
        }

        private void SetPage(int page)
        {
            if (_pages == null || _pages.PageCount == 0)
            {
                return;
            }

#if NET6_0_OR_GREATER
            var newPage = Math.Clamp(page, 1, _pages.PageCount);
#else
            var newPage = Math.Max(1, Math.Min(page, _pages.PageCount));
#endif
            _pageCurrent = newPage;
            PageTextBox.Text = newPage.ToString();
            ReportCanvas.SetPage(newPage - 1);
        }

        private void SetZoom(double zoom)
        {
            _zoom = zoom;
            _zoomMode = ZoomMode.ActualSize;
            ZoomModeComboBox.SelectedItem = _zoomMode;
            ReportCanvas.SetZoom(_zoom);
            UpdateStatusZoom();
        }

        private void ApplyZoomMode()
        {
            if (_pages == null)
            {
                return;
            }

            var viewportWidth = ReportScrollViewer.Viewport.Width;
            var viewportHeight = ReportScrollViewer.Viewport.Height;
            if (viewportWidth <= 1 || viewportHeight <= 1)
            {
                return;
            }

            var pageWidth = _pages.PageWidth;
            var pageHeight = _pages.PageHeight;
            if (pageWidth <= 0 || pageHeight <= 0)
            {
                return;
            }

            switch (_zoomMode)
            {
                case ZoomMode.FitPage:
                    _zoom = Math.Min(viewportWidth / pageWidth, viewportHeight / pageHeight);
                    break;
                case ZoomMode.FitWidth:
                    _zoom = viewportWidth / pageWidth;
                    break;
                case ZoomMode.ActualSize:
                    _zoom = 1.0;
                    break;
            }

            ReportCanvas.SetZoom(_zoom);
            UpdateStatusZoom();
        }

        private void ZoomModeComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ZoomModeComboBox.SelectedItem is ZoomMode mode)
            {
                _zoomMode = mode;
                ApplyZoomMode();
            }
        }

        private void PageTextBoxOnLostFocus(object? sender, RoutedEventArgs e)
        {
            if (int.TryParse(PageTextBox.Text, out var page))
            {
                SetPage(page);
            }
        }

        private async void ApplyParametersButtonOnClick(object? sender, RoutedEventArgs e)
        {
            SetReportParametersAmpersandSeparated(ParametersTextBox.Text ?? string.Empty);
            await RebuildAsync();
        }

        private void ErrorsToggleOnChanged(object? sender, RoutedEventArgs e)
        {
            ErrorsPanel.IsVisible = ErrorsToggleButton.IsChecked == true;
        }
    }
}

