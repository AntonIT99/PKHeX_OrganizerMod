using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

public sealed class OrganizerWindow : Form
{
    private readonly ISaveFileProvider saveFileProvider;
    private readonly TableLayoutPanel layout;
    private readonly ComboBox function;
    private readonly Panel organizeOptionsPanel;
    private readonly ComboBox strategy;
    private readonly Label strategyDescription;
    private readonly Panel typeOptionsPanel;
    private readonly ComboBox layoutMode;
    private readonly Label typeModeDescription;
    private readonly CheckBox groupLegendaries;
    private readonly Panel livingOptionsPanel;
    private readonly ComboBox livingMode;
    private readonly ComboBox shinyScope;
    private readonly ComboBox representativePreference;
    private readonly ComboBox eggHandling;
    private readonly ComboBox invalidHandling;
    private readonly ComboBox overflowOrder;
    private readonly ComboBox overflowStart;
    private readonly Label livingModeDescription;
    private readonly CompetitiveOptionsControl competitiveOptions;
    private readonly Panel competitiveOptionsPanel;
    private readonly CustomRuleEditorControl customOptions;
    private readonly Panel customOptionsPanel;
    private readonly Panel duplicateOptionsPanel;
    private readonly ComboBox duplicateShinyMode;
    private readonly DuplicateCriteriaEditor duplicateCriteria;
    private readonly Panel databaseOptionsPanel;
    private readonly PkmDatabaseImportOptionsControl databaseOptions;
    private readonly Panel pidDuplicateOptionsPanel;
    private readonly Panel teamBuilderOptionsPanel;
    private readonly SmartTeamBuilderOptionsControl teamBuilderOptions;
    private readonly CheckBox renameBoxes;
    private readonly Label renameWarning;
    private readonly CheckBox assignMatchingBackgrounds;
    private readonly CheckBox rotateAlternativeBackgrounds;
    private readonly Label backgroundWarning;
    private readonly Label targetHeading;
    private readonly Label targetExplanation;
    private readonly CheckedListBox targetBoxes;
    private readonly FlowLayoutPanel selectionButtons;
    private readonly FlowLayoutPanel statusPanel;
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
        // The configuration text and selector labels are intentionally descriptive.
        // Give them enough room at the default DPI so users do not need to resize
        // the window before they can read the complete safety boundary wording.
        ClientSize = new Size(980, 780);
        MinimumSize = new Size(850, 650);
        StartPosition = FormStartPosition.CenterParent;

        layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            RowCount = 14,
        };
        for (var row = 0; row < 14; row++)
            layout.RowStyles.Add(new RowStyle(row == 10 ? SizeType.Percent : SizeType.AutoSize, row == 10 ? 100 : 0));

        var heading = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Text = "Organizer Mod",
        };
        saveInformation = new Label { AutoSize = true, Margin = new Padding(0, 6, 0, 12) };

        function = CreateDropDown();
        function.Items.Add("Organize Boxes");
        function.Items.Add("Import from PKM Database");
        function.Items.Add("Remove Duplicate Species");
        function.Items.Add("Remove Duplicates by PID");
        function.Items.Add("Smart Team Builder");

        strategy = CreateDropDown();
        strategy.Items.Add("Type-Optimized Box Allocation");
        strategy.Items.Add("Living Dex Sorting");
        strategy.Items.Add("Competitive / Progress Organizer");
        strategy.Items.Add("Custom Rule-Based Organizer");
        strategyDescription = CreateDescription(string.Empty);

        layoutMode = CreateDropDown();
        layoutMode.Items.Add("Compact");
        layoutMode.Items.Add("Expanded by Type");
        layoutMode.SelectedIndex = 0;
        layoutMode.SelectedIndexChanged += (_, _) => UpdateTypeModeDescription();
        typeModeDescription = CreateDescription(string.Empty);
        groupLegendaries = new CheckBox
        {
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 2),
            Text = "Group Legendary Pokémon into dedicated boxes",
        };
        groupLegendaries.CheckedChanged += (_, _) => UpdateSelectionInformation();
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
        competitiveOptions = new CompetitiveOptionsControl();
        competitiveOptionsPanel = WrapOptions(competitiveOptions);
        customOptions = new CustomRuleEditorControl();
        customOptionsPanel = new Panel
        {
            AutoScroll = true,
            Dock = DockStyle.Top,
            Height = 285,
            Margin = new Padding(0),
        };
        customOptionsPanel.Controls.Add(customOptions);

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
        optionsHost.Controls.Add(customOptionsPanel);
        optionsHost.Controls.Add(competitiveOptionsPanel);
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
        duplicateShinyMode.SelectedIndex = 1;
        duplicateCriteria = new DuplicateCriteriaEditor(
            DuplicateSpeciesRemovalService.GetOriginGames());
        duplicateOptionsPanel = CreateDuplicateOptionsPanel();
        databaseOptions = new PkmDatabaseImportOptionsControl();
        databaseOptionsPanel = WrapOptions(databaseOptions);
        pidDuplicateOptionsPanel = CreatePidDuplicateOptionsPanel();
        teamBuilderOptions = new SmartTeamBuilderOptionsControl(
            DuplicateSpeciesRemovalService.GetOriginGames());
        teamBuilderOptionsPanel = new Panel
        {
            AutoScroll = true,
            Dock = DockStyle.Top,
            Height = 330,
            Margin = new Padding(0),
        };
        teamBuilderOptionsPanel.Controls.Add(teamBuilderOptions);

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
            "Choose a box organizer or a standalone import, duplicate-removal, or Team-building function. Every change is previewed before confirmation."), 1, 1);
        var functionHost = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
        };
        functionHost.Controls.Add(duplicateOptionsPanel);
        functionHost.Controls.Add(databaseOptionsPanel);
        functionHost.Controls.Add(pidDuplicateOptionsPanel);
        functionHost.Controls.Add(teamBuilderOptionsPanel);
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
        assignMatchingBackgrounds = new CheckBox
        {
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 2),
            Text = "Change box backgrounds to match their assigned type",
        };
        rotateAlternativeBackgrounds = new CheckBox
        {
            AutoSize = true,
            Enabled = false,
            Margin = new Padding(22, 0, 0, 2),
            Text = "Use alternative matching backgrounds for additional boxes of the same type",
        };
        backgroundWarning = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(22, 0, 0, 8),
            MaximumSize = new Size(820, 0),
            Text = "Matching backgrounds are assigned only to boxes used by the generated layout. Proposed changes are shown in the preview.",
        };
        assignMatchingBackgrounds.CheckedChanged += (_, _) =>
            rotateAlternativeBackgrounds.Enabled =
                strategy.SelectedIndex == 0 && assignMatchingBackgrounds.Enabled && assignMatchingBackgrounds.Checked;

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

        selectionButtons = new FlowLayoutPanel
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
        statusPanel = new FlowLayoutPanel
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
        // OrganizerWindow is opened modelessly from PKHeX, so DialogResult alone
        // does not close it as it would for a modal dialog.
        closeButton.Click += (_, _) => Close();
        previewButton = new Button { AutoSize = true, Text = "Preview organization…" };
        previewButton.Click += GeneratePreview;
        footer.Controls.Add(closeButton);
        footer.Controls.Add(previewButton);

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(saveInformation, 0, 1);
        layout.Controls.Add(functionOptions, 0, 2);
        layout.Controls.Add(renameBoxes, 0, 3);
        layout.Controls.Add(renameWarning, 0, 4);
        layout.Controls.Add(assignMatchingBackgrounds, 0, 5);
        layout.Controls.Add(rotateAlternativeBackgrounds, 0, 6);
        layout.Controls.Add(backgroundWarning, 0, 7);
        layout.Controls.Add(targetHeading, 0, 8);
        layout.Controls.Add(targetExplanation, 0, 9);
        layout.Controls.Add(targetBoxes, 0, 10);
        layout.Controls.Add(selectionButtons, 0, 11);
        layout.Controls.Add(statusPanel, 0, 12);
        layout.Controls.Add(footer, 0, 13);
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

    public void SelectDuplicateSpeciesFunction() => function.SelectedIndex = 2;
    public void SelectDatabaseImportFunction() => function.SelectedIndex = 1;
    public void SelectPidDuplicateFunction() => function.SelectedIndex = 3;
    public void SelectSmartTeamBuilderFunction() => function.SelectedIndex = 4;

    public void SelectTypeAllocationStrategy()
    {
        function.SelectedIndex = 0;
        strategy.SelectedIndex = 0;
    }

    public void SelectLivingDexSortingStrategy()
    {
        function.SelectedIndex = 0;
        strategy.SelectedIndex = 1;
    }

    public void SelectCompetitiveStrategy()
    {
        function.SelectedIndex = 0;
        strategy.SelectedIndex = 2;
    }

    public void SelectCustomRuleStrategy()
    {
        function.SelectedIndex = 0;
        strategy.SelectedIndex = 3;
    }

    private Panel CreateTypeOptionsPanel()
    {
        var table = CreateOptionsTable(4);
        AddOptionRow(table, 0, "Layout mode:", layoutMode);
        table.Controls.Add(typeModeDescription, 1, 1);
        table.Controls.Add(groupLegendaries, 1, 2);
        table.Controls.Add(CreateDescription(
            "Uses PKHeX's Legendary and Sub-Legendary species categories. Mythical Pokémon, Ultra Beasts, and Paradox Pokémon remain in their type groups."), 1, 3);
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

    private Panel CreatePidDuplicateOptionsPanel()
    {
        var table = CreateOptionsTable(4);
        table.Controls.Add(CreateDescription(
            "Finds cloned Pokémon only when both PID and species match. The fixed scan scope includes the team, every storage box, and pension Pokémon; the box selector does not apply."), 1, 0);
        table.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 2),
            Text = "Representative priority",
        }, 0, 1);
        table.Controls.Add(CreateDescription(
            "1. Pension Pokémon are always kept because pension storage is read-only.\n" +
            "2. Highest level.\n3. Highest experience.\n" +
            "4. Team Pokémon over boxed Pokémon.\n5. A final indistinguishable tie is resolved randomly."), 1, 2);
        table.Controls.Add(CreateDescription(
            "The preview lists every Pokémon to delete, its location, the copy being kept, and all meaningful differences. No sorting or compaction is performed."), 1, 3);
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
        typeOptionsPanel.Visible = strategy.SelectedIndex == 0;
        livingOptionsPanel.Visible = strategy.SelectedIndex == 1;
        competitiveOptionsPanel.Visible = strategy.SelectedIndex == 2;
        customOptionsPanel.Visible = strategy.SelectedIndex == 3;
        strategyDescription.Text = strategy.SelectedIndex switch
        {
            0 => "Assigns Pokémon dynamically so boxes share a common type wherever possible. Dual-type Pokémon may be assigned to either type. No changes are made until the preview is confirmed.",
            1 => "Builds a configurable National Pokédex-ordered collection, chooses one deterministic representative per requested entry, and preserves every extra Pokémon in overflow.",
            2 => "Organizes Pokémon by practical training progress, configurable level bands, or total experience. It does not infer competitive tiers.",
            3 => "Applies up to two ordered grouping rules and four ordered sorting rules. Selected boxes remain the complete source and destination boundary.",
            _ => string.Empty,
        };
        assignMatchingBackgrounds.Text = strategy.SelectedIndex == 0
            ? "Change box backgrounds to match their assigned type"
            : "Change box backgrounds to match generated groups where possible";
        RefreshBoxSelection();
        UpdateBackgroundOptionVisibility();
    }

    private void FunctionChanged()
    {
        var duplicates = function.SelectedIndex == 2;
        var database = function.SelectedIndex == 1;
        var pidDuplicates = function.SelectedIndex == 3;
        var teamBuilder = function.SelectedIndex == 4;
        organizeOptionsPanel.Visible = !duplicates && !database && !pidDuplicates && !teamBuilder;
        duplicateOptionsPanel.Visible = duplicates;
        databaseOptionsPanel.Visible = database;
        pidDuplicateOptionsPanel.Visible = pidDuplicates;
        teamBuilderOptionsPanel.Visible = teamBuilder;
        renameBoxes.Visible = !duplicates && !database && !pidDuplicates && !teamBuilder;
        renameWarning.Visible = !duplicates && !database && !pidDuplicates && !teamBuilder;
        UpdateBackgroundOptionVisibility();
        targetHeading.Visible = !pidDuplicates;
        targetExplanation.Visible = !pidDuplicates;
        targetBoxes.Visible = !pidDuplicates;
        selectionButtons.Visible = !pidDuplicates;
        statusPanel.Visible = true;
        layout.RowStyles[10] = new RowStyle(
            pidDuplicates ? SizeType.AutoSize : SizeType.Percent,
            pidDuplicates ? 0 : 100);
        targetHeading.Text = teamBuilder ? "Boxes available for candidates and Team exchanges" :
            database ? "Save boxes used for comparison and import" : duplicates ? "Boxes to scan for duplicate species" : "Boxes to organize";
        targetExplanation.Text = database
            ? "Pokémon in selected boxes will be checked for conflicts. Empty selected slots receive imports; replacements occur only when previewed. Unselected boxes remain unchanged."
            : teamBuilder ? "The current Team and Pokémon in selected boxes form the candidate pool. Selected boxes may receive displaced Team Pokémon; unselected boxes remain completely unchanged."
            : duplicates ? "Pokémon in selected boxes will be analyzed. Unselected boxes do not participate and remain completely unchanged."
            : "Pokémon in selected boxes will be considered, and those same selected boxes may be reorganized as destinations. Unselected boxes remain completely unchanged.";
        previewButton.Text = teamBuilder ? "Generate Team preview…" : pidDuplicates ? "Preview PID duplicate removal…" : database ? "Scan and generate preview…" : duplicates ? "Preview duplicate removal…" : "Preview organization…";
        RefreshBoxSelection();
    }

    private void UpdateBackgroundOptionVisibility()
    {
        var visible = function.SelectedIndex == 0 && strategy.SelectedIndex != 1;
        assignMatchingBackgrounds.Visible = visible;
        rotateAlternativeBackgrounds.Visible = visible && strategy.SelectedIndex == 0;
        backgroundWarning.Visible = visible;
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
            bool canAssignBackgrounds;
            if (function.SelectedIndex == 3)
            {
                items = [];
                canRename = false;
                canAssignBackgrounds = false;
            }
            else if (function.SelectedIndex == 1)
            {
                var service = new PkmDatabaseImportService(saveFileProvider);
                items = service.GetBoxSelection();
                canRename = false;
                canAssignBackgrounds = false;
            }
            else if (function.SelectedIndex == 2)
            {
                var service = new DuplicateSpeciesRemovalService(saveFileProvider);
                items = service.GetBoxSelection();
                canRename = false;
                canAssignBackgrounds = false;
            }
            else if (function.SelectedIndex == 4)
            {
                var service = new SmartTeamBuilderService(saveFileProvider);
                items = service.GetBoxSelection();
                canRename = false;
                canAssignBackgrounds = false;
                teamBuilderOptions.SetMaximumTeamSize(6);
            }
            else if (strategy.SelectedIndex == 1)
            {
                var service = new LivingDexOrganizationService(saveFileProvider);
                items = service.GetBoxSelection();
                canRename = service.CanRenameBoxes;
                canAssignBackgrounds = false;
            }
            else if (strategy.SelectedIndex is 2 or 3)
            {
                var service = new GroupedOrganizationService(saveFileProvider);
                items = service.GetBoxSelection();
                canRename = service.CanRenameBoxes;
                canAssignBackgrounds = service.CanAssignBackgrounds;
            }
            else
            {
                var service = new TypeOrganizationService(saveFileProvider);
                items = service.GetBoxSelection();
                canRename = service.CanRenameBoxes;
                canAssignBackgrounds = service.CanAssignBackgrounds;
            }
            foreach (var item in items)
                targetBoxes.Items.Add(item, item.IsAvailable);
            renameBoxes.Checked = false;
            renameBoxes.Enabled = canRename;
            assignMatchingBackgrounds.Enabled = canAssignBackgrounds;
            rotateAlternativeBackgrounds.Enabled =
                strategy.SelectedIndex == 0 && canAssignBackgrounds && assignMatchingBackgrounds.Checked;
            backgroundWarning.Text = canAssignBackgrounds
                ? "Matching backgrounds are assigned only to boxes used by the generated layout. Proposed changes are shown in the preview."
                : "Matching backgrounds are unavailable for this save format; existing backgrounds will be preserved.";
        }
        catch (Exception ex)
        {
            selectionLoadError = ex.Message;
            targetBoxes.Items.Clear();
            renameBoxes.Checked = false;
            renameBoxes.Enabled = false;
            assignMatchingBackgrounds.Enabled = false;
            rotateAlternativeBackgrounds.Enabled = false;
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
            if (function.SelectedIndex == 3)
                GeneratePidDuplicatePreview();
            else if (function.SelectedIndex == 4)
                GenerateSmartTeamPreview();
            else if (function.SelectedIndex == 1)
                await GenerateDatabaseImportPreview();
            else if (function.SelectedIndex == 2)
                GenerateDuplicateSpeciesPreview();
            else if (strategy.SelectedIndex == 1)
                GenerateLivingDexPreview();
            else if (strategy.SelectedIndex == 2)
                GenerateCompetitivePreview();
            else if (strategy.SelectedIndex == 3)
                GenerateCustomPreview();
            else
                GenerateTypePreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                $"{function.SelectedItem} failed",
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
        var session = service.CreatePlan(
            selected,
            mode,
            renameBoxes.Checked,
            groupLegendaries.Checked,
            assignMatchingBackgrounds.Enabled && assignMatchingBackgrounds.Checked,
            assignMatchingBackgrounds.Enabled && assignMatchingBackgrounds.Checked &&
            rotateAlternativeBackgrounds.Checked);
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
        if (!ShowErrors(session.Plan.IsValid, session.Plan.Errors, "Living Dex Sorting"))
            return;
        using var preview = new LivingDexPreviewWindow(session);
        if (preview.ShowDialog(this) != DialogResult.OK)
            return;
        service.Apply(session);
        RefreshSaveInfo();
        ShowSuccess(session.Plan.Summary.IncludedPokemon, session.Plan.Summary.RequiredBoxes);
    }

    private void GenerateCompetitivePreview()
    {
        var service = new GroupedOrganizationService(saveFileProvider);
        var options = competitiveOptions.GetOptions(
            renameBoxes.Checked,
            assignMatchingBackgrounds.Enabled && assignMatchingBackgrounds.Checked,
            OrganizationStorageUtilities.GetMaximumBoxNameLength(saveFileProvider.SAV));
        var session = service.CreateCompetitivePlan(GetSelectedBoxes(), options);
        ShowAndApplyGroupedPreview(service, session);
    }

    private void GenerateCustomPreview()
    {
        var service = new GroupedOrganizationService(saveFileProvider);
        var options = customOptions.GetOptions(
            renameBoxes.Checked,
            assignMatchingBackgrounds.Enabled && assignMatchingBackgrounds.Checked,
            OrganizationStorageUtilities.GetMaximumBoxNameLength(saveFileProvider.SAV));
        var session = service.CreateCustomPlan(GetSelectedBoxes(), options);
        ShowAndApplyGroupedPreview(service, session);
    }

    private void ShowAndApplyGroupedPreview(
        GroupedOrganizationService service,
        GroupedOrganizationSession session)
    {
        if (!ShowErrors(session.Plan.IsValid, session.Plan.Errors, session.Plan.StrategyName))
            return;
        using var preview = new GroupedOrganizationPreviewWindow(session);
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

    private void GeneratePidDuplicatePreview()
    {
        var service = new DuplicateRemovalService(saveFileProvider);
        var session = service.CreateSession();
        var plan = session.Plan;
        if (plan.Removals.Count == 0)
        {
            MessageBox.Show(
                this,
                "No Pokémon with both matching PID and species were found across the team, boxes, and pension.",
                "Remove Duplicates by PID",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var preview = new DuplicateRemovalPreviewWindow(
            plan,
            saveFileProvider.SAV.PartyCount);
        if (preview.ShowDialog(this) != DialogResult.OK)
            return;

        var confirm = MessageBox.Show(
            this,
            $"This will remove {plan.Removals.Count} Pokémon from {plan.DuplicateGroupCount} matching PID/species group(s).{Environment.NewLine}" +
            "Pension Pokémon remain read-only. A complete in-memory snapshot will be restored if applying fails, and the save file will not be written automatically.",
            "Confirm PID duplicate removal",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.OK)
            return;

        service.Apply(session);
        RefreshSaveInfo();
        MessageBox.Show(
            this,
            $"Removed {plan.Removals.Count} PID duplicate Pokémon. Review the result before saving in PKHeX.",
            "Remove Duplicates by PID",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void GenerateSmartTeamPreview()
    {
        var service = new SmartTeamBuilderService(saveFileProvider);
        var selected = GetSelectedBoxes();
        var session = service.CreatePlan(selected,
            (boxes, maximum) => teamBuilderOptions.GetOptions(boxes, maximum));
        if (!ShowErrors(session.Plan.IsValid, session.Plan.Errors, "Smart Team Builder"))
            return;
        using var preview = new SmartTeamBuilderPreviewWindow(session);
        if (preview.ShowDialog(this) != DialogResult.OK)
            return;
        var summary = session.Plan.Summary;
        var confirm = MessageBox.Show(
            this,
            $"This will build a Team of {summary.SelectedTeamSize} Pokémon and exchange {summary.MovedFromBoxesToTeam} Pokémon with selected boxes.{Environment.NewLine}" +
            $"{summary.MovedFromTeamToBoxes} current Team Pokémon will move into storage. No Pokémon will be created or deleted.{Environment.NewLine}" +
            "A complete in-memory snapshot will be created first. The save file will not be written to disk automatically.",
            "Confirm Smart Team Builder",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.OK)
            return;
        service.Apply(session);
        RefreshSaveInfo();
        MessageBox.Show(this,
            $"Built a Team of {summary.SelectedTeamSize} Pokémon. Review the Team and selected boxes before saving in PKHeX.",
            "Smart Team Builder", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task GenerateDatabaseImportPreview()
    {
        var service = new PkmDatabaseImportService(saveFileProvider);
        var session = await service.CreatePlanAsync(
            databaseOptions.DatabasePath,
            GetSelectedBoxes(),
            databaseOptions.PidMode,
            databaseOptions.SpeciesMode,
            databaseOptions.SpeciesShinyGrouping,
            databaseOptions.Filters,
            databaseOptions.IncludeTeamInPidComparison,
            databaseOptions.IncludePensionInPidComparison,
            databaseOptions.AllowTeamReplacements,
            databaseOptions.UseTeamSlotsForNewImports);
        if (!ShowErrors(session.Plan.IsValid, session.Plan.Errors, "Import from PKM Database"))
            return;
        using var preview = new PkmDatabaseImportPreviewWindow(session);
        if (preview.ShowDialog(this) != DialogResult.OK)
            return;
        var imports = session.Plan.Imports.Count;
        var replacements = session.Plan.Replacements.Count;
        var teamImports = session.Plan.Imports.Count(x => x.Destination.Area == ExistingPokemonArea.Team);
        var teamReplacements = session.Plan.Replacements.Count(x => x.Existing.Area == ExistingPokemonArea.Team);
        var teamNotice = teamImports + teamReplacements == 0
            ? string.Empty
            : $"{Environment.NewLine}This changes the Team: {teamImports} new member(s), {teamReplacements} replacement(s).";
        var confirm = MessageBox.Show(
            this,
            $"This will import {imports} Pokémon and replace {replacements} existing Pokémon.{Environment.NewLine}" +
            teamNotice +
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
        if (function.SelectedIndex == 3)
        {
            var supported = saveFileProvider.SAV.Generation >= 3;
            selectionInformation.Text =
                "Fixed scope: team · every storage box · pension (read-only priority)";
            validationMessage.Text = supported
                ? "Generate a preview to inspect every keep/remove decision."
                : "Generation 1 and 2 Pokémon do not have meaningful personality IDs.";
            previewButton.Enabled = supported;
            return;
        }

        if (function.SelectedIndex == 4)
        {
            var selectedTeamBoxes = targetBoxes.CheckedItems.OfType<BoxSelectionItem>().Where(x => x.IsAvailable).ToArray();
            var boxPokemon = selectedTeamBoxes.Sum(x => x.PokemonCount);
            var emptySlots = selectedTeamBoxes.Sum(x => 30 - x.PokemonCount);
            var teamCount = saveFileProvider.SAV.HasParty ? saveFileProvider.SAV.PartyCount : 0;
            selectionInformation.Text =
                $"Selected: {selectedTeamBoxes.Length} boxes · {boxPokemon} box Pokémon · {teamCount} current Team Pokémon{Environment.NewLine}" +
                $"Existing empty selected-box slots: {emptySlots} · selected box Pokémon can also vacate exchange slots";
            validationMessage.Text = selectionLoadError ??
                (!saveFileProvider.SAV.HasParty ? "The loaded save does not provide a writable Team."
                    : selectedTeamBoxes.Length == 0 ? "Select at least one available box."
                    : boxPokemon + teamCount == 0 ? "No Team or selected-box Pokémon are available."
                    : "Generate a preview to validate eligibility and exchange capacity.");
            previewButton.Enabled = selectionLoadError is null && saveFileProvider.SAV.HasParty &&
                                    selectedTeamBoxes.Length != 0 && boxPokemon + teamCount != 0;
            return;
        }

        var selected = targetBoxes.CheckedItems
            .OfType<BoxSelectionItem>()
            .Where(item => item.IsAvailable)
            .ToArray();
        var pokemonCount = selected.Sum(item => item.PokemonCount);
        var capacity = selected.Length * LivingDexOrganizationPlanner.BoxCapacity;
        var legendaryCount = function.SelectedIndex == 0 && strategy.SelectedIndex == 0 && groupLegendaries.Checked
            ? CountLegendaryPokemon(selected)
            : 0;
        var regularCount = pokemonCount - legendaryCount;
        var minimumRequired = DivideRoundUp(regularCount, LivingDexOrganizationPlanner.BoxCapacity) +
                              DivideRoundUp(legendaryCount, LivingDexOrganizationPlanner.BoxCapacity);
        var dedicatedCapacityError = minimumRequired > selected.Length;
        selectionInformation.Text = function.SelectedIndex == 1
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
                        : dedicatedCapacityError
                            ? $"Dedicated Legendary boxes require at least {minimumRequired} selected boxes for {legendaryCount} Legendary and {regularCount} other Pokémon."
                        : string.Empty);
        previewButton.Enabled =
            selectionLoadError is null &&
            selected.Length != 0 &&
            pokemonCount != 0 &&
            pokemonCount <= capacity &&
            !dedicatedCapacityError;
        if (function.SelectedIndex == 1)
        {
            previewButton.Enabled = selectionLoadError is null && selected.Length != 0 && databaseOptions.IsDatabaseAvailable;
            if (!databaseOptions.IsDatabaseAvailable)
                validationMessage.Text = $"PKM database directory is unavailable: {databaseOptions.DatabasePath}";
        }
    }

    private int CountLegendaryPokemon(IEnumerable<BoxSelectionItem> selected)
    {
        var save = saveFileProvider.SAV;
        var count = 0;
        foreach (var box in selected)
        {
            for (var slot = 0; slot < save.BoxSlotCount; slot++)
            {
                var entity = save.GetBoxSlotAtIndex(box.BoxIndex, slot);
                if (entity.Species != 0 &&
                    (SpeciesCategory.IsLegendary(entity.Species) || SpeciesCategory.IsSubLegendary(entity.Species)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int DivideRoundUp(int value, int divisor) =>
        value == 0 ? 0 : ((value - 1) / divisor) + 1;

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
            MaximumSize = new Size(820, 0),
            Text = text,
        };
}
