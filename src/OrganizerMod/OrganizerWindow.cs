using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

public sealed class OrganizerWindow : Form
{
    private readonly ISaveFileProvider saveFileProvider;
    private readonly ComboBox function;
    private readonly Panel organizeOptionsPanel;
    private readonly ComboBox strategy;
    private readonly Label strategyDescription;
    private readonly Panel typeOptionsPanel;
    private readonly ComboBox layoutMode;
    private readonly Label typeModeDescription;
    private readonly Panel livingOptionsPanel;
    private readonly ComboBox livingMode;
    private readonly ComboBox shinyScope;
    private readonly ComboBox representativePreference;
    private readonly ComboBox eggHandling;
    private readonly ComboBox invalidHandling;
    private readonly ComboBox overflowOrder;
    private readonly ComboBox overflowStart;
    private readonly Label livingModeDescription;
    private readonly Panel duplicateOptionsPanel;
    private readonly ComboBox duplicateShinyMode;
    private readonly DuplicateCriteriaEditor duplicateCriteria;
    private readonly Panel databaseOptionsPanel;
    private readonly PkmDatabaseImportOptionsControl databaseOptions;
    private readonly CheckBox renameBoxes;
    private readonly Label renameWarning;
    private readonly Label targetHeading;
    private readonly Label targetExplanation;
    private readonly CheckedListBox targetBoxes;
    private readonly Label saveInformation;
    private readonly Label selectionInformation;
    private readonly Label validationMessage;
    private readonly Button previewButton;
    private bool refreshingBoxes;
    private string? selectionLoadError;

    public OrganizerWindow(ISaveFileProvider saveFileProvider)
    {
        this.saveFileProvider = saveFileProvider
            ?? throw new ArgumentNullException(nameof(saveFileProvider));

        Text = "Organizer Mod";
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(820, 780);
        MinimumSize = new Size(720, 650);
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            RowCount = 11,
        };
        for (var row = 0; row < 11; row++)
            layout.RowStyles.Add(new RowStyle(row == 7 ? SizeType.Percent : SizeType.AutoSize, row == 7 ? 100 : 0));

        var heading = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Text = "Organizer Mod",
        };
        saveInformation = new Label { AutoSize = true, Margin = new Padding(0, 6, 0, 12) };

        function = CreateDropDown();
        function.Items.Add("Organize Boxes");
        function.Items.Add("Remove Duplicate Species");
        function.Items.Add("Import from PKM Database");

        strategy = CreateDropDown();
        strategy.Items.Add("Type-Optimized Box Allocation");
        strategy.Items.Add("Living Dex Organizer");
        strategyDescription = CreateDescription(string.Empty);

        layoutMode = CreateDropDown();
        layoutMode.Items.Add("Compact");
        layoutMode.Items.Add("Expanded by Type");
        layoutMode.SelectedIndex = 0;
        layoutMode.SelectedIndexChanged += (_, _) => UpdateTypeModeDescription();
        typeModeDescription = CreateDescription(string.Empty);
        typeOptionsPanel = CreateTypeOptionsPanel();

        livingMode = CreateDropDown();
        livingMode.Items.Add("Species Living Dex");
        livingMode.Items.Add("Form Living Dex");
        livingMode.Items.Add("Shiny Living Dex");
        livingMode.SelectedIndex = 0;
        livingMode.SelectedIndexChanged += (_, _) => UpdateLivingModeControls();
        shinyScope = CreateDropDown();
        shinyScope.Items.Add("One shiny per species");
        shinyScope.Items.Add("One shiny per form");
        shinyScope.SelectedIndex = 0;
        representativePreference = CreateDropDown();
        representativePreference.Items.Add("Default / Safest");
        representativePreference.Items.Add("Oldest obtained");
        representativePreference.Items.Add("Strongest");
        representativePreference.SelectedIndex = 0;
        eggHandling = CreateDropDown();
        eggHandling.Items.Add("Keep eggs in overflow");
        eggHandling.Items.Add("Exclude eggs and leave them in place");
        eggHandling.SelectedIndex = 0;
        invalidHandling = CreateDropDown();
        invalidHandling.Items.Add("Keep in overflow");
        invalidHandling.Items.Add("Exclude and leave in place");
        invalidHandling.SelectedIndex = 0;
        overflowOrder = CreateDropDown();
        overflowOrder.Items.Add("National Dex");
        overflowOrder.Items.Add("Original position");
        overflowOrder.Items.Add("Species then quality");
        overflowOrder.SelectedIndex = 0;
        overflowStart = CreateDropDown();
        overflowStart.Items.Add("Immediately after Living Dex entries");
        overflowStart.Items.Add("At the next box boundary");
        overflowStart.SelectedIndex = 1;
        livingModeDescription = CreateDescription(string.Empty);
        livingOptionsPanel = CreateLivingOptionsPanel();

        var strategyOptions = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
            RowCount = 3,
        };
        strategyOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        strategyOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        strategyOptions.Controls.Add(new Label { AutoSize = true, Text = "Strategy:", Anchor = AnchorStyles.Left }, 0, 0);
        strategyOptions.Controls.Add(strategy, 1, 0);
        strategyOptions.Controls.Add(strategyDescription, 1, 1);
        var optionsHost = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
        };
        optionsHost.Controls.Add(livingOptionsPanel);
        optionsHost.Controls.Add(typeOptionsPanel);
        strategyOptions.Controls.Add(optionsHost, 0, 2);
        strategyOptions.SetColumnSpan(optionsHost, 2);

        renameBoxes = new CheckBox
        {
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 2),
            Text = "Rename affected boxes to reflect the generated layout",
        };
        organizeOptionsPanel = WrapOptions(strategyOptions);

        duplicateShinyMode = CreateDropDown();
        duplicateShinyMode.Items.Add("Consider shiny and non-shiny the same species");
        duplicateShinyMode.Items.Add("Treat shiny and non-shiny separately");
        duplicateShinyMode.Items.Add("Ignore shiny Pokémon");
        duplicateShinyMode.SelectedIndex = 0;
        duplicateCriteria = new DuplicateCriteriaEditor(
            DuplicateSpeciesRemovalService.GetOriginGames());
        duplicateOptionsPanel = CreateDuplicateOptionsPanel();
        databaseOptions = new PkmDatabaseImportOptionsControl();
        databaseOptionsPanel = WrapOptions(databaseOptions);

        var functionOptions = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
            RowCount = 3,
        };
        functionOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        functionOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        functionOptions.Controls.Add(new Label { AutoSize = true, Text = "Function:", Anchor = AnchorStyles.Left }, 0, 0);
        functionOptions.Controls.Add(function, 1, 0);
        functionOptions.Controls.Add(CreateDescription(
            "Organize Boxes creates a previewable target layout. Remove Duplicate Species only clears confirmed duplicate slots and never sorts or compacts boxes."), 1, 1);
        var functionHost = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
        };
        functionHost.Controls.Add(duplicateOptionsPanel);
        functionHost.Controls.Add(databaseOptionsPanel);
        functionHost.Controls.Add(organizeOptionsPanel);
        functionOptions.Controls.Add(functionHost, 0, 2);
        functionOptions.SetColumnSpan(functionHost, 2);

        renameWarning = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(22, 0, 0, 8),
            Text = "Existing and proposed names will be shown and confirmed in the preview.",
        };

        targetHeading = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Text = "Boxes to organize",
        };
        targetExplanation = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 2, 0, 6),
            Text = "Pokémon in selected boxes will be considered, and those same selected boxes may be reorganized as destinations. Unselected boxes remain completely unchanged.",
        };
        targetBoxes = new CheckedListBox
        {
            CheckOnClick = true,
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true,
            IntegralHeight = false,
        };
        targetBoxes.ItemCheck += PreventUnavailableSelection;
        targetBoxes.ItemCheck += UpdateSelectionAfterCheck;

        var selectionButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 6, 0, 6),
        };
        AddSelectionButton(selectionButtons, "Select occupied", item => item.PokemonCount > 0);
        AddSelectionButton(selectionButtons, "Select empty", item => item.PokemonCount == 0);
        AddSelectionButton(selectionButtons, "Select with free slots", item => item.PokemonCount < 30);
        AddSelectionButton(selectionButtons, "Select all", _ => true);
        AddSelectionButton(selectionButtons, "Select none", _ => false);

        selectionInformation = new Label { AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        validationMessage = new Label
        {
            AutoSize = true,
            ForeColor = Color.Firebrick,
            Margin = new Padding(0, 2, 0, 0),
        };
        var statusPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Margin = new Padding(0),
            WrapContents = false,
        };
        statusPanel.Controls.Add(selectionInformation);
        statusPanel.Controls.Add(validationMessage);

        var footer = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 8, 0, 0),
        };
        var closeButton = new Button { AutoSize = true, DialogResult = DialogResult.Cancel, Text = "Close" };
        previewButton = new Button { AutoSize = true, Text = "Preview organization…" };
        previewButton.Click += GeneratePreview;
        footer.Controls.Add(closeButton);
        footer.Controls.Add(previewButton);

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(saveInformation, 0, 1);
        layout.Controls.Add(functionOptions, 0, 2);
        layout.Controls.Add(renameBoxes, 0, 3);
        layout.Controls.Add(renameWarning, 0, 4);
        layout.Controls.Add(targetHeading, 0, 5);
        layout.Controls.Add(targetExplanation, 0, 6);
        layout.Controls.Add(targetBoxes, 0, 7);
        layout.Controls.Add(selectionButtons, 0, 8);
        layout.Controls.Add(statusPanel, 0, 9);
        layout.Controls.Add(footer, 0, 10);
        Controls.Add(layout);
        CancelButton = closeButton;

        function.SelectedIndexChanged += (_, _) => FunctionChanged();
        strategy.SelectedIndexChanged += (_, _) => StrategyChanged();
        strategy.SelectedIndex = 0;
        function.SelectedIndex = 0;
        UpdateTypeModeDescription();
        UpdateLivingModeControls();
        RefreshSaveInfo();
    }

    public void RefreshSaveInfo()
    {
        try
        {
            var save = saveFileProvider.SAV;
            saveInformation.Text =
                $"Loaded: {save.GetType().Name} — {save.Version}, generation {save.Generation}, trainer {save.OT}";
            RefreshBoxSelection();
        }
        catch (Exception ex)
        {
            saveInformation.Text = $"Save information is unavailable: {ex.Message}";
            targetBoxes.Items.Clear();
            renameBoxes.Enabled = false;
            selectionLoadError = ex.Message;
            UpdateSelectionInformation();
        }
    }

    public void SelectDuplicateSpeciesFunction() => function.SelectedIndex = 1;
    public void SelectDatabaseImportFunction() => function.SelectedIndex = 2;

    private Panel CreateTypeOptionsPanel()
    {
        var table = CreateOptionsTable(2);
        AddOptionRow(table, 0, "Layout mode:", layoutMode);
        table.Controls.Add(typeModeDescription, 1, 1);
        return WrapOptions(table);
    }

    private Panel CreateLivingOptionsPanel()
    {
        var table = CreateOptionsTable(9);
        AddOptionRow(table, 0, "Living Dex mode:", livingMode);
        table.Controls.Add(livingModeDescription, 1, 1);
        AddOptionRow(table, 2, "Shiny scope:", shinyScope);
        AddOptionRow(table, 3, "Representative preference:", representativePreference);
        AddOptionRow(table, 4, "Egg handling:", eggHandling);
        AddOptionRow(table, 5, "Invalid Pokémon:", invalidHandling);
        AddOptionRow(table, 6, "Overflow order:", overflowOrder);
        AddOptionRow(table, 7, "Start Living Dex at:", CreateFixedStartSelector());
        AddOptionRow(table, 8, "Start overflow:", overflowStart);
        return WrapOptions(table);
    }

    private Panel CreateDuplicateOptionsPanel()
    {
        var table = CreateOptionsTable(5);
        table.Controls.Add(CreateDescription(
            "Groups selected-box Pokémon by species ID. Alternate forms count as the same species. Eggs and invalid entries remain untouched."), 1, 0);
        AddOptionRow(table, 1, "Shiny handling:", duplicateShinyMode);
        table.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 2),
            Text = "Representative criteria, highest priority first",
        }, 0, 2);
        table.SetColumnSpan(table.GetControlFromPosition(0, 2)!, 2);
        table.Controls.Add(duplicateCriteria, 0, 3);
        table.SetColumnSpan(duplicateCriteria, 2);
        table.Controls.Add(CreateDescription(
            "Disable any criterion or reorder with ↑ and ↓. If all criteria tie—or none are enabled—the earliest original box and slot is kept."), 1, 4);
        return WrapOptions(table);
    }

    private static TableLayoutPanel CreateOptionsTable(int rows)
    {
        var table = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
            RowCount = rows,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static Panel WrapOptions(Control content)
    {
        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
        };
        panel.Controls.Add(content);
        return panel;
    }

    private static void AddOptionRow(
        TableLayoutPanel table,
        int row,
        string label,
        Control control)
    {
        table.Controls.Add(new Label { AutoSize = true, Text = label, Anchor = AnchorStyles.Left }, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private static ComboBox CreateFixedStartSelector()
    {
        var selector = CreateDropDown();
        selector.Items.Add("First slot of first selected box");
        selector.SelectedIndex = 0;
        selector.Enabled = false;
        return selector;
    }

    private void StrategyChanged()
    {
        var living = strategy.SelectedIndex == 1;
        typeOptionsPanel.Visible = !living;
        livingOptionsPanel.Visible = living;
        strategyDescription.Text = living
            ? "Builds a configurable National Pokédex-ordered collection, chooses one deterministic representative per requested entry, and preserves every extra Pokémon in overflow."
            : "Assigns Pokémon dynamically so boxes share a common type wherever possible. Dual-type Pokémon may be assigned to either type. No changes are made until the preview is confirmed.";
        RefreshBoxSelection();
    }

    private void FunctionChanged()
    {
        var duplicates = function.SelectedIndex == 1;
        var database = function.SelectedIndex == 2;
        organizeOptionsPanel.Visible = !duplicates && !database;
        duplicateOptionsPanel.Visible = duplicates;
        databaseOptionsPanel.Visible = database;
        renameBoxes.Visible = !duplicates && !database;
        renameWarning.Visible = !duplicates && !database;
        targetHeading.Text = database ? "Save boxes used for comparison and import" : duplicates ? "Boxes to scan for duplicate species" : "Boxes to organize";
        targetExplanation.Text = database
            ? "Pokémon in selected boxes will be checked for conflicts. Empty selected slots receive imports; replacements occur only when previewed. Unselected boxes remain unchanged."
            : duplicates ? "Pokémon in selected boxes will be analyzed. Unselected boxes do not participate and remain completely unchanged."
            : "Pokémon in selected boxes will be considered, and those same selected boxes may be reorganized as destinations. Unselected boxes remain completely unchanged.";
        previewButton.Text = database ? "Scan and generate preview…" : duplicates ? "Preview duplicate removal…" : "Preview organization…";
        RefreshBoxSelection();
    }

    private void UpdateTypeModeDescription()
    {
        typeModeDescription.Text = layoutMode.SelectedIndex == 1
            ? "Creates separate boxes for each represented type when enough boxes are available."
            : "Prioritizes full single-type boxes and combines inefficient leftovers into mixed boxes.";
    }

    private void UpdateLivingModeControls()
    {
        shinyScope.Enabled = livingMode.SelectedIndex == 2;
        livingModeDescription.Text = livingMode.SelectedIndex switch
        {
            0 => "Keeps one representative per National Dex species. Alternate forms satisfy their species entry; additional copies go to overflow.",
            1 => "Keeps one representative for each collectible stored form reported by PKHeX. Battle-only and fused forms are excluded.",
            2 => "Keeps one shiny representative per species or collectible form. Non-shiny Pokémon go to overflow; shiny-lock coverage is explained in the preview.",
            _ => string.Empty,
        };
    }

    private void RefreshBoxSelection()
    {
        refreshingBoxes = true;
        try
        {
            selectionLoadError = null;
            targetBoxes.Items.Clear();
            IReadOnlyList<BoxSelectionItem> items;
            bool canRename;
            if (function.SelectedIndex == 2)
            {
                var service = new PkmDatabaseImportService(saveFileProvider);
                items = service.GetBoxSelection();
                canRename = false;
            }
            else if (function.SelectedIndex == 1)
            {
                var service = new DuplicateSpeciesRemovalService(saveFileProvider);
                items = service.GetBoxSelection();
                canRename = false;
            }
            else if (strategy.SelectedIndex == 1)
            {
                var service = new LivingDexOrganizationService(saveFileProvider);
                items = service.GetBoxSelection();
                canRename = service.CanRenameBoxes;
            }
            else
            {
                var service = new TypeOrganizationService(saveFileProvider);
                items = service.GetBoxSelection();
                canRename = service.CanRenameBoxes;
            }
            foreach (var item in items)
                targetBoxes.Items.Add(item, item.IsAvailable);
            renameBoxes.Checked = false;
            renameBoxes.Enabled = canRename;
        }
        catch (Exception ex)
        {
            selectionLoadError = ex.Message;
            targetBoxes.Items.Clear();
            renameBoxes.Checked = false;
            renameBoxes.Enabled = false;
        }
        finally
        {
            refreshingBoxes = false;
            UpdateSelectionInformation();
        }
    }

    private async void GeneratePreview(object? sender, EventArgs e)
    {
        try
        {
            UseWaitCursor = true;
            previewButton.Enabled = false;
            if (function.SelectedIndex == 2)
                await GenerateDatabaseImportPreview();
            else if (function.SelectedIndex == 1)
                GenerateDuplicateSpeciesPreview();
            else if (strategy.SelectedIndex == 1)
                GenerateLivingDexPreview();
            else
                GenerateTypePreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                $"{strategy.SelectedItem} failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            UpdateSelectionInformation();
        }
    }

    private void GenerateTypePreview()
    {
        var selected = GetSelectedBoxes();
        var mode = layoutMode.SelectedIndex == 1
            ? TypeBoxLayoutMode.ExpandedByType
            : TypeBoxLayoutMode.Compact;
        var service = new TypeOrganizationService(saveFileProvider);
        var session = service.CreatePlan(selected, mode, renameBoxes.Checked);
        if (!ShowErrors(session.Plan.IsValid, session.Plan.Errors, "Type-Optimized Box Allocation"))
            return;
        using var preview = new TypeOrganizationPreviewWindow(session);
        if (preview.ShowDialog(this) != DialogResult.OK)
            return;
        service.Apply(session);
        RefreshSaveInfo();
        ShowSuccess(session.Plan.PokemonCount, session.Plan.Summary.UsedBoxes);
    }

    private void GenerateLivingDexPreview()
    {
        var selected = GetSelectedBoxes();
        var options = new LivingDexOrganizerOptions(
            (LivingDexMode)livingMode.SelectedIndex,
            (LivingDexShinyScope)shinyScope.SelectedIndex,
            (LivingDexRepresentativePreference)representativePreference.SelectedIndex,
            (LivingDexEggHandling)eggHandling.SelectedIndex,
            (LivingDexInvalidHandling)invalidHandling.SelectedIndex,
            (LivingDexOverflowOrder)overflowOrder.SelectedIndex,
            (LivingDexOverflowStart)overflowStart.SelectedIndex,
            renameBoxes.Checked,
            OrganizationStorageUtilities.GetMaximumBoxNameLength(saveFileProvider.SAV));
        var service = new LivingDexOrganizationService(saveFileProvider);
        var session = service.CreatePlan(selected, options);
        if (!ShowErrors(session.Plan.IsValid, session.Plan.Errors, "Living Dex Organizer"))
            return;
        using var preview = new LivingDexPreviewWindow(session);
        if (preview.ShowDialog(this) != DialogResult.OK)
            return;
        service.Apply(session);
        RefreshSaveInfo();
        ShowSuccess(session.Plan.Summary.IncludedPokemon, session.Plan.Summary.RequiredBoxes);
    }

    private void GenerateDuplicateSpeciesPreview()
    {
        var service = new DuplicateSpeciesRemovalService(saveFileProvider);
        var session = service.CreatePlan(
            GetSelectedBoxes(),
            (ShinyDuplicateMode)duplicateShinyMode.SelectedIndex,
            duplicateCriteria.GetCriteria());
        if (!ShowErrors(
                session.Plan.IsValid,
                session.Plan.Errors,
                "Remove Duplicate Species"))
            return;
        if (session.Plan.RemovalCandidates.Count == 0)
        {
            MessageBox.Show(
                this,
                "No duplicate species were found with the selected boxes and shiny handling.",
                "Remove Duplicate Species",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }
        using var preview = new DuplicateSpeciesPreviewWindow(session);
        if (preview.ShowDialog(this) != DialogResult.OK)
            return;
        var count = session.Plan.RemovalCandidates.Count;
        var confirm = MessageBox.Show(
            this,
            $"This will clear {count} Pokémon from the selected boxes.{Environment.NewLine}" +
            "An in-memory save snapshot will be created first and restored if applying fails. " +
            "The save file will not be written to disk automatically.",
            "Confirm duplicate removal",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.OK)
            return;
        service.Apply(session);
        RefreshSaveInfo();
        MessageBox.Show(
            this,
            $"Removed {count} duplicate Pokémon. Save the file in PKHeX only after reviewing the result.",
            "Remove Duplicate Species",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async Task GenerateDatabaseImportPreview()
    {
        var service = new PkmDatabaseImportService(saveFileProvider);
        var session = await service.CreatePlanAsync(
            databaseOptions.DatabasePath,
            GetSelectedBoxes(),
            databaseOptions.PidMode,
            databaseOptions.SpeciesMode,
            databaseOptions.Filters);
        if (!ShowErrors(session.Plan.IsValid, session.Plan.Errors, "Import from PKM Database"))
            return;
        using var preview = new PkmDatabaseImportPreviewWindow(session);
        if (preview.ShowDialog(this) != DialogResult.OK)
            return;
        var imports = session.Plan.Imports.Count;
        var replacements = session.Plan.Replacements.Count;
        var confirm = MessageBox.Show(
            this,
            $"This will import {imports} Pokémon and replace {replacements} existing Pokémon.{Environment.NewLine}" +
            "A complete in-memory snapshot will preserve replaced Pokémon and roll back failures. The save file will not be written automatically.",
            "Confirm PKM database import",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.OK)
            return;
        service.Apply(session);
        RefreshSaveInfo();
        MessageBox.Show(this, $"Imported {imports} and replaced {replacements} Pokémon. Review the save before exporting it.", "Import from PKM Database", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private bool ShowErrors(
        bool isValid,
        IReadOnlyList<string> errors,
        string title)
    {
        if (isValid)
            return true;
        MessageBox.Show(
            this,
            string.Join(Environment.NewLine, errors),
            $"{title} — Cannot create plan",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    private void ShowSuccess(int pokemonCount, int boxes)
    {
        MessageBox.Show(
            this,
            $"Organized {pokemonCount} Pokémon across {boxes} boxes. Save the file in PKHeX to persist the changes.",
            "Organizer Mod",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private int[] GetSelectedBoxes() =>
        targetBoxes.CheckedItems
            .OfType<BoxSelectionItem>()
            .Where(item => item.IsAvailable)
            .Select(item => item.BoxIndex)
            .ToArray();

    private void PreventUnavailableSelection(object? sender, ItemCheckEventArgs e)
    {
        if (refreshingBoxes || e.NewValue == CheckState.Unchecked)
            return;
        if (targetBoxes.Items[e.Index] is BoxSelectionItem { IsAvailable: false })
            e.NewValue = CheckState.Unchecked;
    }

    private void UpdateSelectionAfterCheck(object? sender, ItemCheckEventArgs e)
    {
        if (refreshingBoxes || !IsHandleCreated)
            return;
        BeginInvoke(UpdateSelectionInformation);
    }

    private void AddSelectionButton(
        Control parent,
        string text,
        Func<BoxSelectionItem, bool> predicate)
    {
        var button = new Button { AutoSize = true, Text = text };
        button.Click += (_, _) => SetChecks(predicate);
        parent.Controls.Add(button);
    }

    private void SetChecks(Func<BoxSelectionItem, bool> shouldCheck)
    {
        refreshingBoxes = true;
        try
        {
            for (var index = 0; index < targetBoxes.Items.Count; index++)
            {
                var item = (BoxSelectionItem)targetBoxes.Items[index]!;
                targetBoxes.SetItemChecked(index, item.IsAvailable && shouldCheck(item));
            }
        }
        finally
        {
            refreshingBoxes = false;
            UpdateSelectionInformation();
        }
    }

    private void UpdateSelectionInformation()
    {
        var selected = targetBoxes.CheckedItems
            .OfType<BoxSelectionItem>()
            .Where(item => item.IsAvailable)
            .ToArray();
        var pokemonCount = selected.Sum(item => item.PokemonCount);
        var capacity = selected.Length * LivingDexOrganizationPlanner.BoxCapacity;
        var minimumRequired = pokemonCount == 0
            ? 0
            : ((pokemonCount - 1) / LivingDexOrganizationPlanner.BoxCapacity) + 1;
        selectionInformation.Text = function.SelectedIndex == 2
            ? $"Selected boxes: {selected.Length} · Existing Pokémon: {pokemonCount} · Empty slots: {capacity - pokemonCount}"
            : $"Selected: {selected.Length} boxes · {pokemonCount} Pokémon · {capacity} slots{Environment.NewLine}" +
              $"Minimum required: {minimumRequired} boxes · Free capacity: {capacity - pokemonCount} slots";

        validationMessage.Text = selectionLoadError ??
            (selected.Length == 0
                ? "Select at least one available box."
                : pokemonCount == 0
                    ? "No Pokémon found in the selected boxes."
                    : pokemonCount > capacity
                        ? $"Insufficient capacity: {pokemonCount} Pokémon require more than {capacity} selected slots."
                        : string.Empty);
        previewButton.Enabled =
            selectionLoadError is null &&
            selected.Length != 0 &&
            pokemonCount != 0 &&
            pokemonCount <= capacity;
        if (function.SelectedIndex == 2)
        {
            previewButton.Enabled = selectionLoadError is null && selected.Length != 0 && databaseOptions.IsDatabaseAvailable;
            if (!databaseOptions.IsDatabaseAvailable)
                validationMessage.Text = $"PKM database directory is unavailable: {databaseOptions.DatabasePath}";
        }
    }

    private static ComboBox CreateDropDown() =>
        new()
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(8, 3, 0, 3),
        };

    private static Label CreateDescription(string text) =>
        new()
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(8, 0, 0, 6),
            MaximumSize = new Size(670, 0),
            Text = text,
        };
}
