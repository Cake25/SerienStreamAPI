using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SerienStreamAPI.Client;
using SerienStreamAPI.Enums;
using SerienStreamAPI.Models;
using Terminal.Gui;

namespace SerienStreamAPI.Gui;

internal static class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Application.Init();
        try
        {
            new SerienStreamGui().Run();
        }
        finally
        {
            Application.Shutdown();
        }
    }
}

internal sealed class SerienStreamGui
{
    readonly TextField hostUrlField;
    readonly RadioGroup siteRadioGroup;
    readonly CheckBox ignoreCertificateCheckbox;
    readonly TextField titleField;
    readonly Button searchButton;
    readonly TextView seriesInfoView;
    readonly ListView seasonsListView;
    readonly ListView episodesListView;
    readonly ListView streamsListView;
    readonly StatusItem statusItem;

    SerienStreamClient? client;
    Series? currentSeries;
    Media[] currentEpisodes = [];
    VideoDetails? currentVideoDetails;

    public SerienStreamGui()
    {
        var menu = new MenuBar(new MenuBarItem[]
        {
            new("_File", new MenuItem[]
            {
                new("_Quit", "", () => Application.RequestStop())
            })
        });

        hostUrlField = new TextField("https://s.to/")
        {
            X = 12,
            Y = 0,
            Width = 40,
        };

        siteRadioGroup = new RadioGroup(new[] { "Serie", "Anime" })
        {
            X = 12,
            Y = Pos.Bottom(hostUrlField),
        };

        ignoreCertificateCheckbox = new CheckBox("Ignore certificate validation errors")
        {
            X = 12,
            Y = Pos.Bottom(siteRadioGroup),
            Checked = false
        };

        titleField = new TextField()
        {
            X = 12,
            Y = Pos.Bottom(ignoreCertificateCheckbox) + 1,
            Width = 40,
        };

        searchButton = new Button("Search")
        {
            X = Pos.Right(titleField) + 1,
            Y = Pos.Top(titleField),
        };

        seriesInfoView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true
        };

        seasonsListView = new ListView(new List<string>())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false
        };

        episodesListView = new ListView(new List<string>())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false
        };

        streamsListView = new ListView(new List<string>())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false
        };

        statusItem = new(StatusItem.DefaultHotKey, "Ready", null);

        BuildLayout(menu);
        RegisterEvents();
    }

    public void Run() => Application.Run();

    void BuildLayout(MenuBar menu)
    {
        var window = new Window("SerienStreamAPI Browser")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1,
        };

        var hostLabel = new Label("Host URL:") { X = 0, Y = 0 };
        var siteLabel = new Label("Site:") { X = 0, Y = Pos.Bottom(hostLabel) };
        var titleLabel = new Label("Title:") { X = 0, Y = Pos.Bottom(ignoreCertificateCheckbox) + 1 };

        var infoFrame = new FrameView("Series details")
        {
            X = 0,
            Y = Pos.Bottom(titleLabel) + 1,
            Width = Dim.Percent(45),
            Height = 12
        };
        infoFrame.Add(seriesInfoView);

        var seasonsFrame = new FrameView("Seasons/Movies")
        {
            X = 0,
            Y = Pos.Bottom(infoFrame) + 1,
            Width = Dim.Percent(25),
            Height = Dim.Fill()
        };
        seasonsFrame.Add(seasonsListView);

        var episodesFrame = new FrameView("Episodes")
        {
            X = Pos.Right(seasonsFrame) + 1,
            Y = Pos.Top(seasonsFrame),
            Width = Dim.Percent(35),
            Height = Dim.Fill()
        };
        episodesFrame.Add(episodesListView);

        var streamsFrame = new FrameView("Streams")
        {
            X = Pos.Right(episodesFrame) + 1,
            Y = Pos.Top(seasonsFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        streamsFrame.Add(streamsListView);

        var statusBar = new StatusBar(new[] { statusItem });

        window.Add(hostLabel, hostUrlField, siteLabel, siteRadioGroup, ignoreCertificateCheckbox, titleLabel, titleField, searchButton, infoFrame, seasonsFrame, episodesFrame, streamsFrame);
        Application.Top.Add(menu, window, statusBar);
    }

    void RegisterEvents()
    {
        searchButton.Clicked += async () => await SearchSeriesAsync();
        seasonsListView.SelectedItemChanged += async args => await LoadEpisodesForSelectionAsync(args.Item);
        episodesListView.SelectedItemChanged += async args => await LoadStreamsForSelectionAsync(args.Item);
    }

    async Task SearchSeriesAsync()
    {
        var title = titleField.Text.ToString().Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.ErrorQuery("Missing title", "Please enter a series or anime title to search.", "Ok");
            return;
        }

        UpdateStatus("Searching...");
        DisableInteraction();

        try
        {
            client = new SerienStreamClient(hostUrlField.Text.ToString(), GetSelectedSite(), ignoreCertificateCheckbox.Checked);
            currentSeries = await client.GetSeriesAsync(title);

            Application.MainLoop.Invoke(() =>
            {
                seriesInfoView.Text = BuildSeriesDescription(currentSeries);
                seasonsListView.SetSource(BuildSeasonsList(currentSeries));
                episodesListView.SetSource(new List<string>());
                streamsListView.SetSource(new List<string>());
            });

            await LoadEpisodesForSelectionAsync(seasonsListView.SelectedItem);
            UpdateStatus("Ready");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Search failed", ex.Message, "Close");
            UpdateStatus("Ready");
        }
        finally
        {
            EnableInteraction();
        }
    }

    async Task LoadEpisodesForSelectionAsync(int selectedIndex)
    {
        if (client is null || currentSeries is null)
        {
            return;
        }

        int seasonNumber = MapSelectionToSeason(selectedIndex, currentSeries.HasMovies);
        UpdateStatus($"Loading {(seasonNumber == 0 ? "movies" : "season " + seasonNumber)}...");

        try
        {
            currentEpisodes = await client.GetEpisodesAsync(currentSeries.Title, seasonNumber);
            Application.MainLoop.Invoke(() =>
            {
                episodesListView.SetSource(BuildEpisodeList(currentEpisodes));
                streamsListView.SetSource(new List<string>());
            });

            if (currentEpisodes.Length > 0)
            {
                await LoadStreamsForSelectionAsync(episodesListView.SelectedItem);
            }
            UpdateStatus("Ready");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Episode loading failed", ex.Message, "Close");
            UpdateStatus("Ready");
        }
    }

    async Task LoadStreamsForSelectionAsync(int selectedIndex)
    {
        if (client is null || currentSeries is null || selectedIndex < 0 || selectedIndex >= currentEpisodes.Length)
        {
            return;
        }

        Media episode = currentEpisodes[selectedIndex];
        int seasonNumber = MapSelectionToSeason(seasonsListView.SelectedItem, currentSeries.HasMovies);
        UpdateStatus($"Loading streams for {episode.Title}...");

        try
        {
            currentVideoDetails = await client.GetEpisodeVideoInfoAsync(currentSeries.Title, episode.Number, seasonNumber);
            Application.MainLoop.Invoke(() =>
            {
                streamsListView.SetSource(BuildStreamList(currentVideoDetails.Streams));
            });
            UpdateStatus("Ready");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Stream loading failed", ex.Message, "Close");
            UpdateStatus("Ready");
        }
    }

    string[] BuildSeasonsList(Series series)
    {
        var seasons = new List<string>();
        if (series.HasMovies)
        {
            seasons.Add("Movies");
        }

        for (int i = 1; i <= series.SeasonsCount; i++)
        {
            seasons.Add($"Season {i}");
        }

        return seasons.ToArray();
    }

    string[] BuildEpisodeList(Media[] episodes) =>
        episodes.Select(episode => $"[{episode.Number}] {episode.Title}").ToArray();

    string[] BuildStreamList(VideoStream[] streams) =>
        streams.Select(stream => $"{stream.Hoster} - Audio: {stream.Language.Audio}, Subtitles: {stream.Language.Subtitle ?? Language.Unknown}").ToArray();

    static string BuildSeriesDescription(Series series)
    {
        var builder = new StringBuilder();
        builder.AppendLine(series.Title);
        builder.AppendLine(new string('-', series.Title.Length));
        builder.AppendLine(series.Description);
        builder.AppendLine();
        builder.AppendLine($"Years: {series.YearStart} - {(series.YearEnd?.ToString() ?? "Today")}");
        builder.AppendLine($"Seasons: {series.SeasonsCount}");
        builder.AppendLine($"Movies available: {(series.HasMovies ? "Yes" : "No")}");
        builder.AppendLine($"Genres: {string.Join(", ", series.Genres)}");
        builder.AppendLine($"Country: {string.Join(", ", series.CountriesOfOrigin)}");
        builder.AppendLine($"Cast: {string.Join(", ", series.Actors)}");

        return builder.ToString();
    }

    string GetSelectedSite() => siteRadioGroup.SelectedItem == 1 ? "anime" : "serie";

    static int MapSelectionToSeason(int selectedIndex, bool hasMovies)
    {
        if (selectedIndex < 0)
        {
            return hasMovies ? 0 : 1;
        }

        if (hasMovies)
        {
            return selectedIndex;
        }

        return selectedIndex + 1;
    }

    void UpdateStatus(string message)
    {
        Application.MainLoop.Invoke(() => statusItem.Title = message);
    }

    void DisableInteraction()
    {
        Application.MainLoop.Invoke(() =>
        {
            searchButton.Enabled = false;
            hostUrlField.Enabled = false;
            siteRadioGroup.Enabled = false;
            ignoreCertificateCheckbox.Enabled = false;
            titleField.Enabled = false;
        });
    }

    void EnableInteraction()
    {
        Application.MainLoop.Invoke(() =>
        {
            searchButton.Enabled = true;
            hostUrlField.Enabled = true;
            siteRadioGroup.Enabled = true;
            ignoreCertificateCheckbox.Enabled = true;
            titleField.Enabled = true;
        });
    }
}
