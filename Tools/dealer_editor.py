import json
import tkinter as tk
from tkinter import ttk, filedialog, messagebox, simpledialog
import os # Needed for path operations
import re # Needed for filename sanitization
import traceback # For better error reporting

# --- Custom Dialog for Merge Conflicts ---
class MergeConflictDialog(simpledialog.Dialog):
    """
    Custom dialog to ask the user how to resolve a merge conflict
    when a dealer with the same name already exists.
    """
    def __init__(self, parent, title, dealer_name):
        self.dealer_name = dealer_name
        self.result = "cancel" # Default action if dialog is closed
        super().__init__(parent, title)

    def body(self, master):
        """Creates the dialog body."""
        tk.Label(master, text=f"Dealer '{self.dealer_name}' already exists in the current data.").grid(row=0, columnspan=3, pady=10, padx=10)
        tk.Label(master, text="How would you like to proceed?").grid(row=1, columnspan=3, padx=10)
        return None # No specific widget to focus on initially

    def buttonbox(self):
        """Creates the dialog buttons."""
        box = ttk.Frame(self)

        keep_button = ttk.Button(box, text="Keep Existing", width=15, command=self.keep_existing)
        keep_button.pack(side=tk.LEFT, padx=5, pady=10)
        overwrite_button = ttk.Button(box, text="Overwrite with New", width=18, command=self.overwrite)
        overwrite_button.pack(side=tk.LEFT, padx=5, pady=10)
        cancel_button = ttk.Button(box, text="Cancel Merge", width=15, command=self.cancel_merge)
        cancel_button.pack(side=tk.LEFT, padx=5, pady=10)

        self.bind("<Return>", lambda event: self.keep_existing())
        self.bind("<Escape>", self.cancel_merge)

        box.pack()

    def keep_existing(self, event=None):
        """Sets result to 'keep' and closes dialog."""
        self.result = "keep"
        self.ok()

    def overwrite(self, event=None):
        """Sets result to 'overwrite' and closes dialog."""
        self.result = "overwrite"
        self.ok()

    def cancel_merge(self, event=None):
        """Sets result to 'cancel' and closes dialog using standard cancel method."""
        self.result = "cancel"
        self.cancel()

class DealerEditorApp:
    """
    Main application class for the Empire JSON Editor.
    Handles UI creation, data management, and file operations for global settings
    and dealer data stored in JSON format.
    """
    def __init__(self, root):
        """
        Initializes the application.

        Args:
            root: The main Tkinter window (tk.Tk() instance).
        """
        self.root = root
        self.root.title("Empire Editor")
        self.root.minsize(950, 720)

        # Initialize data structure
        self.data = {"dealers": []}
        self.other_top_level_keys = {}
        
        # State tracking variables
        self.current_dealer_index = -1
        self.current_drug_index = -1
        self.current_quality_index = -1
        self.current_effect_index = -1
        self.current_shipping_index = -1
        
        self.file_path = None

        self.create_widgets()
        self.new_file(confirm=False) # Load default empty structure on start

    # --- UI Creation Methods ---
    def create_widgets(self):
        """Creates the main application widgets and layout."""
        self._create_menu()

        main_notebook = ttk.Notebook(self.root)
        main_notebook.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        global_settings_tab = ttk.Frame(main_notebook, padding=(10))
        dealers_tab = ttk.Frame(main_notebook)
        
        main_notebook.add(global_settings_tab, text="Global Settings")
        main_notebook.add(dealers_tab, text="Dealers")

        self._create_global_settings_section(global_settings_tab)
        self._create_dealers_section(dealers_tab)

    def _create_menu(self):
        """Creates the application's menu bar."""
        menubar = tk.Menu(self.root)
        filemenu = tk.Menu(menubar, tearoff=0)
        filemenu.add_command(label="New", command=self.new_file, accelerator="Ctrl+N")
        filemenu.add_command(label="Open...", command=self.load_json, accelerator="Ctrl+O")
        filemenu.add_command(label="Save", command=self.save_json, accelerator="Ctrl+S")
        filemenu.add_command(label="Save As...", command=self.save_json_as, accelerator="Ctrl+Shift+S")
        filemenu.add_separator()
        filemenu.add_command(label="Split Dealers...", command=self.split_dealers)
        filemenu.add_command(label="Combine Dealers...", command=self.combine_dealers)
        filemenu.add_command(label="Merge empire.json...", command=self.merge_dealers)
        filemenu.add_separator()
        filemenu.add_command(label="Exit", command=self.root.quit)
        menubar.add_cascade(label="File", menu=filemenu)
        self.root.config(menu=menubar)

        self.root.bind_all("<Control-n>", lambda e: self.new_file())
        self.root.bind_all("<Control-o>", lambda e: self.load_json())
        self.root.bind_all("<Control-s>", lambda e: self.save_json())
        self.root.bind_all("<Control-S>", lambda e: self.save_json_as())


    def _create_global_settings_section(self, parent_tab):
        """Creates the UI for editing global JSON settings."""
        canvas = tk.Canvas(parent_tab)
        scrollbar = ttk.Scrollbar(parent_tab, orient="vertical", command=canvas.yview)
        scrollable_frame = ttk.Frame(canvas)

        scrollable_frame.bind("<Configure>", lambda e: canvas.configure(scrollregion=canvas.bbox("all")))
        canvas.create_window((0, 0), window=scrollable_frame, anchor="nw")
        canvas.configure(yscrollcommand=scrollbar.set)

        # --- Version ---
        version_frame = ttk.LabelFrame(scrollable_frame, text="Version", padding=10)
        version_frame.grid(row=0, column=0, padx=5, pady=5, sticky="ew")
        version_frame.columnconfigure(1, weight=1)
        self.version_s1api_entry = self._create_label_entry_pair(version_frame, "S1 API:", 0)
        self.version_empire_entry = self._create_label_entry_pair(version_frame, "Empire:", 1)

        # --- Arrays/Lists ---
        self.global_text_widgets = {}
        array_fields = {
            "effectsName": ("Effect Names", str),
            "effectsDollarMult": ("Effect Dollar Multipliers", float),
            "qualityTypes": ("Quality Types", str),
            "qualitiesDollarMult": ("Quality Dollar Multipliers", float),
            "productTypes": ("Product Types", str),
            "randomNumberRanges": ("Random Number Ranges", float)
        }
        
        row_num = 1
        for key, (label, type_cast) in array_fields.items():
            frame = ttk.LabelFrame(scrollable_frame, text=label, padding=10)
            frame.grid(row=row_num, column=0, padx=5, pady=5, sticky="nsew")
            frame.columnconfigure(0, weight=1)
            text_widget = tk.Text(frame, height=8, width=40, wrap=tk.NONE)
            text_widget.grid(row=0, column=0, sticky="nsew")
            text_scrollbar_y = ttk.Scrollbar(frame, orient=tk.VERTICAL, command=text_widget.yview)
            text_scrollbar_y.grid(row=0, column=1, sticky="ns")
            text_widget.configure(yscrollcommand=text_scrollbar_y.set)
            self.global_text_widgets[key] = text_widget
            row_num += 1

        scrollable_frame.columnconfigure(0, weight=1)
        canvas.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")
        
    def _create_dealers_section(self, parent_tab):
        """Creates the main paned window layout for the dealer editor."""
        main_pane = ttk.PanedWindow(parent_tab, orient=tk.HORIZONTAL)
        main_pane.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        self._create_dealer_list_section(main_pane)
        self._create_details_section(main_pane)

    def _create_dealer_list_section(self, parent_pane):
        """Creates the section for displaying and managing the list of dealers."""
        dealers_frame = ttk.LabelFrame(parent_pane, text="Dealers", padding=(10, 5))
        parent_pane.add(dealers_frame, weight=1)

        dealer_list_controls_frame = ttk.Frame(dealers_frame)
        dealer_list_controls_frame.pack(fill=tk.BOTH, expand=True)

        dealer_list_frame = ttk.Frame(dealer_list_controls_frame)
        dealer_list_frame.pack(fill=tk.BOTH, expand=True, side=tk.LEFT, padx=(0,5))
        dealer_scrollbar = ttk.Scrollbar(dealer_list_frame, orient=tk.VERTICAL)
        self.dealer_list = tk.Listbox(dealer_list_frame, yscrollcommand=dealer_scrollbar.set, exportselection=False)
        dealer_scrollbar.config(command=self.dealer_list.yview)
        dealer_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.dealer_list.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        self.dealer_list.bind('<<ListboxSelect>>', self.load_selected_dealer)

        dealer_reorder_frame = ttk.Frame(dealer_list_controls_frame)
        dealer_reorder_frame.pack(fill=tk.Y, side=tk.RIGHT)
        move_up_button = ttk.Button(dealer_reorder_frame, text="▲", command=self.move_dealer_up, width=3)
        move_up_button.pack(pady=2, padx=2)
        move_down_button = ttk.Button(dealer_reorder_frame, text="▼", command=self.move_dealer_down, width=3)
        move_down_button.pack(pady=2, padx=2)

        dealer_button_frame = ttk.Frame(dealers_frame)
        dealer_button_frame.pack(fill=tk.X, pady=5)
        add_dealer_button = ttk.Button(dealer_button_frame, text="Add Dealer", command=self.add_dealer)
        add_dealer_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(0, 2))
        remove_dealer_button = ttk.Button(dealer_button_frame, text="Remove Dealer", command=self.remove_dealer)
        remove_dealer_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(2, 0))

    def _create_details_section(self, parent_pane):
        """Creates the scrollable section for displaying dealer details."""
        details_outer_frame = ttk.Frame(parent_pane)
        parent_pane.add(details_outer_frame, weight=3)
        
        details_canvas = tk.Canvas(details_outer_frame)
        details_scrollbar = ttk.Scrollbar(details_outer_frame, orient="vertical", command=details_canvas.yview)
        self.details_scrollable_frame = ttk.Frame(details_canvas, padding=(10, 5))

        self.details_scrollable_frame.bind("<Configure>", lambda e: details_canvas.configure(scrollregion=details_canvas.bbox("all")))
        details_canvas.bind_all("<MouseWheel>", lambda e: details_canvas.yview_scroll(int(-1*(e.delta/120)), "units"))
        details_canvas.bind_all("<Button-4>", lambda e: details_canvas.yview_scroll(-1, "units"))
        details_canvas.bind_all("<Button-5>", lambda e: details_canvas.yview_scroll(1, "units"))

        details_canvas.create_window((0, 0), window=self.details_scrollable_frame, anchor="nw")
        details_canvas.configure(yscrollcommand=details_scrollbar.set)
        
        details_canvas.pack(side="left", fill="both", expand=True)
        details_scrollbar.pack(side="right", fill="y")

        self.create_dealer_details_widgets(self.details_scrollable_frame)

    def create_dealer_details_widgets(self, parent_frame):
        """Creates all widgets for editing a selected dealer's details."""
        current_row = 0

        basic_info_frame = ttk.LabelFrame(parent_frame, text="Basic Info", padding=(10, 5))
        basic_info_frame.grid(row=current_row, column=0, padx=5, pady=5, sticky="ew"); current_row += 1
        basic_info_frame.columnconfigure(1, weight=1)
        
        self.name_entry = self._create_label_entry_pair(basic_info_frame, "Name (str):", 0)
        self.image_entry = self._create_label_entry_pair(basic_info_frame, "Image (str):", 1)
        self.tier_entry = self._create_label_entry_pair(basic_info_frame, "Tier (int):", 2)
        
        unlock_req_outer_label = ttk.Label(basic_info_frame, text="Unlock Requirements (JSON objects, one per line):")
        unlock_req_outer_label.grid(row=3, column=0, padx=5, pady=2, sticky="nw")
        
        unlock_req_text_frame = ttk.Frame(basic_info_frame)
        unlock_req_text_frame.grid(row=3, column=1, padx=5, pady=2, sticky="ew")
        unlock_req_text_frame.columnconfigure(0, weight=1)

        self.unlock_requirements_text = tk.Text(unlock_req_text_frame, height=4, width=30, wrap=tk.WORD)
        self.unlock_requirements_text.grid(row=0, column=0, sticky="ew")
        unlock_req_scrollbar = ttk.Scrollbar(unlock_req_text_frame, orient=tk.VERTICAL, command=self.unlock_requirements_text.yview)
        unlock_req_scrollbar.grid(row=0, column=1, sticky="ns")
        self.unlock_requirements_text.configure(yscrollcommand=unlock_req_scrollbar.set)
        
        self.rep_log_base_entry = self._create_label_entry_pair(basic_info_frame, "Rep Log Base (int):", 4)
        self.deal_days_entry = self._create_label_entry_pair(basic_info_frame, "Deal Days (str, comma-sep):", 5)
        
        # --- Curfew Deal (Boolean) ---
        ttk.Label(basic_info_frame, text="Curfew Deal:").grid(row=6, column=0, padx=5, pady=2, sticky="w")
        self.curfew_deal_var = tk.StringVar()
        self.curfew_deal_combobox = ttk.Combobox(basic_info_frame, textvariable=self.curfew_deal_var, values=["True", "False"], state="readonly")
        self.curfew_deal_combobox.grid(row=6, column=1, padx=5, pady=2, sticky="ew")
        self.curfew_deal_combobox.set("False") # Default value

        # --- Gift Frame ---
        gift_frame = ttk.LabelFrame(parent_frame, text="Gift", padding=(10, 5))
        gift_frame.grid(row=current_row, column=0, padx=5, pady=5, sticky="ew"); current_row += 1
        gift_frame.columnconfigure(1, weight=1)
        self.gift_cost_entry = self._create_label_entry_pair(gift_frame, "Cost (int):", 0)
        self.gift_rep_entry = self._create_label_entry_pair(gift_frame, "Rep (int):", 1)

        # --- Reward Frame ---
        reward_frame = ttk.LabelFrame(parent_frame, text="Reward", padding=(10, 5))
        reward_frame.grid(row=current_row, column=0, padx=5, pady=5, sticky="ew"); current_row += 1
        reward_frame.columnconfigure(1, weight=1)
        self.reward_rep_cost_entry = self._create_label_entry_pair(reward_frame, "Rep Cost (int):", 0)
        self.reward_unlock_rep_entry = self._create_label_entry_pair(reward_frame, "Unlock Rep (int):", 1)
        self.reward_type_entry = self._create_label_entry_pair(reward_frame, "Type (str):", 2)
        self.reward_args_entry = self._create_label_entry_pair(reward_frame, "Args (str, comma-sep):", 3)

        deals_frame = ttk.LabelFrame(parent_frame, text="Deals (Each line: int, float, int, int)", padding=(10,5))
        deals_frame.grid(row=current_row, column=0, padx=5, pady=5, sticky="nsew"); current_row +=1
        deals_frame.columnconfigure(0, weight=1)
        self.deals_text = tk.Text(deals_frame, height=5, width=40, wrap=tk.WORD)
        self.deals_text.grid(row=0, column=0, padx=5, pady=5, sticky="ew")
        deals_scrollbar = ttk.Scrollbar(deals_frame, orient=tk.VERTICAL, command=self.deals_text.yview)
        deals_scrollbar.grid(row=0, column=1, sticky="ns")
        self.deals_text.configure(yscrollcommand=deals_scrollbar.set)

        self.drugs_list, self.drug_details_frame = self._create_list_detail_section(
            parent_frame, current_row, "Drugs", "Drug",
            self.add_drug, self.remove_drug, self.load_selected_drug,
            self.create_drug_details_widgets
        ); current_row += 1
        
        self.shipping_list, self.shipping_details_frame = self._create_list_detail_section(
            parent_frame, current_row, "Shipping", "Shipping Option",
            self.add_shipping, self.remove_shipping, self.load_selected_shipping,
            self.create_shipping_details_widgets
        ); current_row += 1

        dialogue_frame = ttk.LabelFrame(parent_frame, text="Dialogue (One line per entry)", padding=(10, 5))
        dialogue_frame.grid(row=current_row, column=0, padx=5, pady=5, sticky="nsew"); current_row += 1
        self.create_dialogue_details_widgets(dialogue_frame)

        save_dealer_button = ttk.Button(parent_frame, text="Save Current Dealer", command=self.save_current_dealer)
        save_dealer_button.grid(row=current_row, column=0, pady=15, padx=5, sticky="ew"); current_row += 1
        
        parent_frame.columnconfigure(0, weight=1)

    def _create_label_entry_pair(self, parent, label_text, row_num):
        """Helper to create a Label and Entry pair."""
        ttk.Label(parent, text=label_text).grid(row=row_num, column=0, padx=5, pady=2, sticky="w")
        entry = ttk.Entry(parent)
        entry.grid(row=row_num, column=1, padx=5, pady=2, sticky="ew")
        return entry

    def _create_list_detail_section(self, parent_frame, grid_row, section_label, item_name, add_cmd, remove_cmd, select_cmd, create_detail_widgets_cmd):
        """Helper to create a standard listbox with add/remove buttons and a details frame."""
        frame = ttk.LabelFrame(parent_frame, text=section_label, padding=(10, 5))
        frame.grid(row=grid_row, column=0, padx=5, pady=5, sticky="nsew")
        frame.columnconfigure(0, weight=1)

        list_frame = ttk.Frame(frame)
        list_frame.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 5))
        list_frame.columnconfigure(0, weight=1)
        scrollbar = ttk.Scrollbar(list_frame, orient=tk.VERTICAL)
        listbox = tk.Listbox(list_frame, height=5, yscrollcommand=scrollbar.set, exportselection=False)
        scrollbar.config(command=listbox.yview); scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        listbox.bind('<<ListboxSelect>>', select_cmd)

        button_frame = ttk.Frame(frame)
        button_frame.grid(row=1, column=0, columnspan=2, sticky="ew", pady=2)
        ttk.Button(button_frame, text=f"Add {item_name}", command=add_cmd).pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(0, 2))
        ttk.Button(button_frame, text=f"Remove {item_name}", command=remove_cmd).pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(2, 0))

        details_frame_widget = ttk.LabelFrame(frame, text=f"{item_name} Details", padding=(10, 5))
        details_frame_widget.grid(row=2, column=0, columnspan=2, padx=5, pady=5, sticky="ew")
        create_detail_widgets_cmd(details_frame_widget)

        return listbox, details_frame_widget


    def create_drug_details_widgets(self, parent_frame):
        """Creates widgets for editing drug details (Schema Aligned)."""
        parent_frame.columnconfigure(1, weight=1); current_row = 0
        self.drug_type_entry = self._create_label_entry_pair(parent_frame, "Type (str):", current_row); current_row +=1
        self.drug_unlock_rep_entry = self._create_label_entry_pair(parent_frame, "Unlock Rep (int):", current_row); current_row +=1
        self.drug_base_dollar_entry = self._create_label_entry_pair(parent_frame, "Base Dollar (int):", current_row); current_row +=1
        self.drug_base_rep_entry = self._create_label_entry_pair(parent_frame, "Base Rep (int):", current_row); current_row +=1
        self.drug_base_xp_entry = self._create_label_entry_pair(parent_frame, "Base XP (int):", current_row); current_row +=1
        self.drug_rep_mult_entry = self._create_label_entry_pair(parent_frame, "Rep Mult (float):", current_row); current_row +=1
        self.drug_xp_mult_entry = self._create_label_entry_pair(parent_frame, "XP Mult (float):", current_row); current_row +=1

        self.qualities_list, self.quality_details_frame = self._create_list_detail_section(
            parent_frame, current_row, "Qualities", "Quality",
            self.add_quality, self.remove_quality, self.load_selected_quality,
            self.create_quality_details_widgets
        ); current_row +=1

        self.effects_list, self.effect_details_frame = self._create_list_detail_section(
            parent_frame, current_row, "Effects", "Effect",
            self.add_effect, self.remove_effect, self.load_selected_effect,
            self.create_effect_details_widgets
        ); current_row +=1
        
        self.set_widget_state(parent_frame, 'disabled')

    def create_quality_details_widgets(self, parent_frame):
        """Creates widgets for editing quality details (Schema Aligned)."""
        parent_frame.columnconfigure(1, weight=1); current_row = 0
        self.quality_type_entry = self._create_label_entry_pair(parent_frame, "Type (str):", current_row); current_row+=1
        self.quality_dollar_mult_entry = self._create_label_entry_pair(parent_frame, "Dollar Mult (float):", current_row); current_row+=1
        self.quality_unlock_rep_entry = self._create_label_entry_pair(parent_frame, "Unlock Rep (int):", current_row); current_row+=1
        self.set_widget_state(parent_frame, 'disabled')

    def create_effect_details_widgets(self, parent_frame):
        """Creates widgets for editing effect details (Schema Aligned)."""
        parent_frame.columnconfigure(1, weight=1); current_row = 0
        self.effect_name_entry = self._create_label_entry_pair(parent_frame, "Name (str):", current_row); current_row+=1
        self.effect_unlock_rep_entry = self._create_label_entry_pair(parent_frame, "Unlock Rep (int):", current_row); current_row+=1
        self.effect_probability_entry = self._create_label_entry_pair(parent_frame, "Probability (float):", current_row); current_row+=1
        self.effect_dollar_mult_entry = self._create_label_entry_pair(parent_frame, "Dollar Mult (float):", current_row); current_row+=1
        self.set_widget_state(parent_frame, 'disabled')

    def create_shipping_details_widgets(self, parent_frame):
        """Creates widgets for editing shipping details (Schema Aligned)."""
        parent_frame.columnconfigure(1, weight=1); current_row = 0
        self.shipping_name_entry = self._create_label_entry_pair(parent_frame, "Name (str):", current_row); current_row+=1
        self.shipping_cost_entry = self._create_label_entry_pair(parent_frame, "Cost (int):", current_row); current_row+=1
        self.shipping_unlock_rep_entry = self._create_label_entry_pair(parent_frame, "Unlock Rep (int):", current_row); current_row+=1
        self.shipping_min_amount_entry = self._create_label_entry_pair(parent_frame, "Min Amount (int):", current_row); current_row+=1
        self.shipping_step_amount_entry = self._create_label_entry_pair(parent_frame, "Step Amount (int):", current_row); current_row+=1
        self.shipping_max_amount_entry = self._create_label_entry_pair(parent_frame, "Max Amount (int):", current_row); current_row+=1
        self.shipping_deal_modifier_entry = self._create_label_entry_pair(parent_frame, "Deal Modifier (4 floats, comma-sep):", current_row); current_row+=1
        self.set_widget_state(parent_frame, 'disabled')

    def create_dialogue_details_widgets(self, parent_frame):
        """Creates Text widgets for editing dialogue arrays (Schema Aligned)."""
        parent_frame.columnconfigure(1, weight=1); parent_frame.columnconfigure(3, weight=1)
        text_height = 3
        dialogue_fields = ["intro", "dealStart", "accept", "incomplete", "expire", "fail", "success", "reward"]
        self.dialogue_text_widgets = {}

        for i, field_name in enumerate(dialogue_fields):
            col = 0 if i < (len(dialogue_fields) / 2) else 2
            row_offset = i % (len(dialogue_fields) // 2)
            label_text = ' '.join(word.capitalize() for word in re.findall(r'[A-Z]?[a-z]+|[A-Z]+(?=[A-Z]|$)', field_name)) + ":"
            
            ttk.Label(parent_frame, text=label_text).grid(row=row_offset, column=col, padx=5, pady=2, sticky="nw")
            text_widget = tk.Text(parent_frame, height=text_height, width=25, wrap=tk.WORD)
            text_widget.grid(row=row_offset, column=col+1, padx=5, pady=2, sticky="ew")
            
            text_scrollbar = ttk.Scrollbar(parent_frame, orient=tk.VERTICAL, command=text_widget.yview)
            text_scrollbar.grid(row=row_offset, column=col+2, sticky="nsw", padx=(0,5))
            text_widget.configure(yscrollcommand=text_scrollbar.set)
            
            self.dialogue_text_widgets[field_name] = text_widget

    # --- Utility Methods ---
    def set_widget_state(self, parent_widget, state):
        """Recursively sets the state ('normal' or 'disabled') for child widgets, skipping containers."""
        if isinstance(parent_widget, (ttk.Frame, ttk.LabelFrame, tk.Frame, tk.Canvas)):
             pass
        else:
            try: parent_widget.configure(state=state)
            except tk.TclError: pass
        
        for child in parent_widget.winfo_children():
            if not isinstance(child, (ttk.Scrollbar, tk.Scrollbar)):
                self.set_widget_state(child, state)

    def clear_entry(self, entry):
        if entry: entry.delete(0, tk.END)
    
    def clear_text(self, text_widget):
        if text_widget: text_widget.delete('1.0', tk.END)
    
    def clear_listbox(self, listbox):
        if listbox: listbox.delete(0, tk.END)

    def sanitize_filename(self, name):
        if not name: return "unnamed_dealer"
        name = str(name)
        name = re.sub(r'[\\/*?:"<>|]', "", name)
        name = name.replace(" ", "_")
        return name[:100]

    def safe_float(self, value_str, default=0.0):
        try: return float(value_str)
        except (ValueError, TypeError): return default

    def safe_int(self, value_str, default=0):
        try: return int(float(value_str))
        except (ValueError, TypeError): return default

    def list_from_string(self, value_str, item_type=str, delimiter=','):
        if not value_str: return []
        items = [item.strip() for item in value_str.split(delimiter) if item.strip()]
        try:
            if item_type == int: return [self.safe_int(item) for item in items]
            if item_type == float: return [self.safe_float(item) for item in items]
            return [str(item) for item in items]
        except ValueError:
            messagebox.showerror("Conversion Error", f"Could not convert all items in '{value_str}' to {item_type.__name__} using delimiter '{delimiter}'.")
            return []

    def string_from_list(self, value_list, delimiter=', '):
        return delimiter.join(map(str, value_list)) if isinstance(value_list, list) else ""

    def list_from_text(self, text_widget, item_type=str):
        content = text_widget.get("1.0", tk.END).strip()
        lines = [line.strip() for line in content.split('\n') if line.strip()] if content else []
        try:
            return [item_type(line) for line in lines]
        except (ValueError, TypeError):
            messagebox.showerror("Type Conversion Error", f"Could not convert all lines to the required type ({item_type.__name__}).\nPlease check the input for errors.")
            return None

    def text_from_list(self, text_widget, value_list):
        self.clear_text(text_widget)
        if isinstance(value_list, list):
            text_widget.insert("1.0", "\n".join(map(str, value_list)))

    def deals_from_text(self, text_widget):
        lines = self.list_from_text(text_widget, item_type=str)
        if lines is None: return None # Error occurred in list_from_text
        parsed_deals = []
        for i, line in enumerate(lines):
            parts = [p.strip() for p in line.split(',')]
            if len(parts) == 4:
                try:
                    parsed_deals.append([
                        self.safe_int(parts[0]), self.safe_float(parts[1]),
                        self.safe_int(parts[2]), self.safe_int(parts[3])
                    ])
                except ValueError:
                    messagebox.showerror("Deal Parse Error", f"Error parsing deal on line {i+1}: '{line}'. Expected int,float,int,int format.")
                    return None
            elif line:
                messagebox.showerror("Deal Parse Error", f"Incorrect format for deal on line {i+1}: '{line}'. Expected 4 comma-separated values.")
                return None
        return parsed_deals

    def text_from_deals(self, text_widget, deals_list):
        self.clear_text(text_widget)
        if isinstance(deals_list, list):
            lines = [", ".join(map(str, deal_item)) for deal_item in deals_list if isinstance(deal_item, list) and len(deal_item) == 4]
            text_widget.insert("1.0", "\n".join(lines))
            
    def text_from_list_of_json_objects(self, text_widget, list_of_objects):
        self.clear_text(text_widget)
        if isinstance(list_of_objects, list):
            try:
                lines = [json.dumps(obj, sort_keys=True, indent=None) for obj in list_of_objects]
                text_widget.insert("1.0", "\n".join(lines))
            except TypeError as e:
                messagebox.showerror("JSON Error", f"Could not serialize object to JSON for display: {e}\nObject: {obj}")
                text_widget.insert("1.0", "\n".join(map(str, list_of_objects)))


    def list_of_json_objects_from_text(self, text_widget):
        content = text_widget.get("1.0", tk.END).strip()
        lines = [line.strip() for line in content.split('\n') if line.strip()] if content else []
        parsed_objects = []
        for i, line_str in enumerate(lines):
            try:
                if line_str:
                    parsed_obj = json.loads(line_str)
                    parsed_objects.append(parsed_obj)
            except json.JSONDecodeError as e:
                messagebox.showerror("JSON Parse Error", f"Error parsing an Unlock Requirement on line {i+1} as JSON: {e}\n\nContent: '{line_str}'\n\nPlease ensure each line is a valid JSON object (e.g., {{\"key\": \"value\"}}).")
                return None
        return parsed_objects


    # --- File Operations ---
    def update_dealer_listbox(self, select_index=None):
        current_selection_value = None
        if select_index is None:
            selected_indices = self.dealer_list.curselection()
            if selected_indices:
                current_selection_value = selected_indices[0]
        else:
            current_selection_value = select_index

        self.clear_listbox(self.dealer_list)
        for i, dealer in enumerate(self.data.get("dealers", [])):
            self.dealer_list.insert(tk.END, f"{i}: {dealer.get('name', 'Unnamed Dealer')}")

        if current_selection_value is not None and 0 <= current_selection_value < self.dealer_list.size():
            self.dealer_list.selection_set(current_selection_value)
            self.dealer_list.activate(current_selection_value)
            self.dealer_list.see(current_selection_value)
        else:
            self.clear_dealer_details()

    def get_default_data_structure(self):
        """Returns a dictionary with the default empty structure for a new file."""
        return {
            "version": {"s1api": "1.0.0", "empire": "0.1"},
            "effectsName": [], "effectsDollarMult": [],
            "qualityTypes": [], "qualitiesDollarMult": [],
            "productTypes": [], "randomNumberRanges": [],
            "dealers": []
        }

    def new_file(self, event=None, confirm=True):
        if confirm and not messagebox.askyesno("Confirm New File", "Discard all current data and start a new file?"):
            return
        
        default_data = self.get_default_data_structure()
        self.data = {"dealers": default_data.pop("dealers")}
        self.other_top_level_keys = default_data
        self.current_dealer_index = -1
        self.file_path = None
        
        self._load_all_data_to_ui()
        self.root.title("Empire Editor - New File")

    def load_json(self, event=None):
        path = filedialog.askopenfilename(
            title="Open JSON File",
            defaultextension=".json",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
        )
        if not path: return

        try:
            with open(path, 'r', encoding='utf-8') as f:
                loaded_full_structure = json.load(f)
            
            if not isinstance(loaded_full_structure, dict):
                messagebox.showerror("Load Error", "Invalid JSON structure. Root must be an object (dictionary).")
                return

            dealers_list = loaded_full_structure.get("dealers")
            if dealers_list is not None and isinstance(dealers_list, list):
                self.data["dealers"] = dealers_list
                self.other_top_level_keys = {k: v for k, v in loaded_full_structure.items() if k != "dealers"}
            elif "name" in loaded_full_structure:
                if not self.file_path and not self.data["dealers"]:
                    self.data["dealers"] = [loaded_full_structure]
                    self.other_top_level_keys = {}
                    messagebox.showinfo("File Loaded", f"Loaded '{os.path.basename(path)}' as a single dealer. It will be wrapped in a 'dealers' list and standard structure if saved as an empire file.")
                else:
                    if messagebox.askyesno("Load Single Dealer", f"'{os.path.basename(path)}' appears to be a single dealer object. Merge it into the current data?"):
                        self.data['dealers'].append(loaded_full_structure)
            else:
                self.data["dealers"] = []
                self.other_top_level_keys = loaded_full_structure
                messagebox.showinfo("File Loaded", "No 'dealers' key found. Loaded other top-level keys. Dealers list is now empty.")

            
            self.file_path = path
            self.root.title(f"Empire Editor - {os.path.basename(self.file_path)}")
            self.current_dealer_index = -1
            self._load_all_data_to_ui()

        except json.JSONDecodeError: messagebox.showerror("Load Error", f"Could not decode JSON from file: {path}")
        except Exception as e: messagebox.showerror("Load Error", f"An unexpected error occurred: {e}\n{traceback.format_exc()}")

    def save_json(self, event=None):
        if not self.file_path:
            self.save_json_as()
        else:
            self._save_to_path(self.file_path)

    def save_json_as(self, event=None):
        path = filedialog.asksaveasfilename(
            title="Save Empire JSON As...",
            defaultextension=".json",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")],
            initialfile= os.path.basename(self.file_path) if self.file_path else "empire.json"
        )
        if not path: return
        self._save_to_path(path)

    def _save_to_path(self, path, show_success_msg=True):
        if not self._sync_all_ui_to_data():
            messagebox.showerror("Save Error", "Could not save due to invalid data in one of the fields. Please check the error messages and try again.")
            return False
        
        data_to_save = self.other_top_level_keys.copy()
        data_to_save["dealers"] = self.data.get("dealers", [])

        try:
            with open(path, 'w', encoding='utf-8') as f:
                json.dump(data_to_save, f, indent=4)
            self.file_path = path
            self.root.title(f"Empire Editor - {os.path.basename(self.file_path)}")
            if show_success_msg:
                 messagebox.showinfo("Save Success", f"Data saved successfully to {path}")
            return True
        except Exception as e:
            messagebox.showerror("Save Error", f"An error occurred while saving to {path}: {e}\n{traceback.format_exc()}")
            return False

    # --- Data Sync and UI Population ---
    def _load_all_data_to_ui(self):
        """Loads all in-memory data (global and dealers) into the UI widgets."""
        self._load_global_settings_to_ui()
        self.update_dealer_listbox()
        self.clear_dealer_details()

    def _load_global_settings_to_ui(self):
        """Populates the Global Settings tab from self.other_top_level_keys."""
        # Version
        version_data = self.other_top_level_keys.get('version', {})
        self.clear_entry(self.version_s1api_entry); self.version_s1api_entry.insert(0, version_data.get('s1api', ''))
        self.clear_entry(self.version_empire_entry); self.version_empire_entry.insert(0, version_data.get('empire', ''))

        # Array fields
        for key, widget in self.global_text_widgets.items():
            self.text_from_list(widget, self.other_top_level_keys.get(key, []))

    def _sync_all_ui_to_data(self):
        """Saves all data from the UI back into the internal data structures."""
        if not self._save_global_settings_from_ui(): return False
        if self.current_dealer_index != -1:
            if not self._save_dealer_data(self.current_dealer_index, show_success=False):
                return False
        return True

    def _save_global_settings_from_ui(self):
        """Saves data from the Global Settings tab into self.other_top_level_keys."""
        self.other_top_level_keys['version'] = {
            "s1api": self.version_s1api_entry.get(),
            "empire": self.version_empire_entry.get()
        }
        
        array_fields = {
            "effectsName": str, "effectsDollarMult": float, "qualityTypes": str,
            "qualitiesDollarMult": float, "productTypes": str, "randomNumberRanges": float
        }
        
        for key, item_type in array_fields.items():
            widget = self.global_text_widgets[key]
            parsed_list = self.list_from_text(widget, item_type)
            if parsed_list is None: # An error occurred during parsing
                messagebox.showerror("Save Error", f"Invalid data in the '{key}' field. Changes not saved.")
                return False
            self.other_top_level_keys[key] = parsed_list
        return True

    # --- Advanced File Operations: Split, Combine, Merge ---
    def split_dealers(self):
        if not self.data.get("dealers"):
            messagebox.showwarning("Split Error", "No dealers loaded to split.")
            return

        if not self._sync_all_ui_to_data():
            messagebox.showerror("Split Error", "Could not save current changes. Splitting aborted.")
            return

        output_dir = filedialog.askdirectory(title="Select Directory to Save Split Dealer Files")
        if not output_dir: return

        success_count = 0; error_count = 0
        for i, dealer_obj in enumerate(self.data["dealers"]):
            dealer_name = dealer_obj.get('name', f'Unnamed_Dealer_{i}')
            filename = self.sanitize_filename(dealer_name) + ".json"
            filepath = os.path.join(output_dir, filename)
            
            # The other_top_level_keys are now already synced from the UI
            dealer_file_content = self.other_top_level_keys.copy()
            dealer_file_content["dealers"] = [dealer_obj]
            
            try:
                with open(filepath, 'w', encoding='utf-8') as f:
                    json.dump(dealer_file_content, f, indent=4)
                success_count += 1
            except Exception as e:
                error_count += 1
                print(f"Error saving split file {filepath}: {e}\n{traceback.format_exc()}")

        msg = f"Splitting operation finished.\nSuccessfully saved: {success_count} dealer files.\nFailed: {error_count} files."
        if error_count > 0:
            messagebox.showerror("Split Complete (with errors)", msg + "\nPlease check the console for error details.")
        else:
            messagebox.showinfo("Split Complete", msg + f"\nFiles saved to directory:\n{output_dir}")

    def combine_dealers(self):
        files_to_combine = filedialog.askopenfilenames(
            title="Select Dealer JSON Files to Combine",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
        )
        if not files_to_combine: return

        combined_dealers_list = []
        loaded_dealer_names = set()
        errors_loading_files = []
        first_file_other_keys = None

        for filepath in files_to_combine:
            try:
                with open(filepath, 'r', encoding='utf-8') as f:
                    file_content = json.load(f)
                
                if not isinstance(file_content, dict):
                    errors_loading_files.append(f"Not an object: {os.path.basename(filepath)}")
                    continue

                current_file_other_keys = {k:v for k,v in file_content.items() if k != "dealers"}
                if first_file_other_keys is None and current_file_other_keys:
                    first_file_other_keys = current_file_other_keys
                
                dealers_from_this_file = []
                if isinstance(file_content.get("dealers"), list):
                    dealers_from_this_file = file_content["dealers"]
                elif "name" in file_content:
                    dealers_from_this_file = [file_content]
                
                for dealer_obj in dealers_from_this_file:
                    if isinstance(dealer_obj, dict) and "name" in dealer_obj:
                        name = dealer_obj['name']
                        if name not in loaded_dealer_names:
                            combined_dealers_list.append(dealer_obj)
                            loaded_dealer_names.add(name)
                    else:
                        errors_loading_files.append(f"Invalid dealer structure in {os.path.basename(filepath)}")
            except Exception as e:
                errors_loading_files.append(f"Error reading {os.path.basename(filepath)}: {e}")

        if not combined_dealers_list:
            messagebox.showerror("Combine Error", "No valid dealer data found in the selected files.")
            return
        
        final_other_keys = first_file_other_keys if first_file_other_keys is not None else {}
        final_combined_data_to_save = final_other_keys.copy()
        final_combined_data_to_save["dealers"] = combined_dealers_list

        report_message = f"Found {len(combined_dealers_list)} unique dealers to combine."
        if errors_loading_files:
            report_message += "\n\nErrors encountered while loading some files:\n- " + "\n- ".join(errors_loading_files)
            messagebox.showwarning("Combine Issues", report_message)
        else:
            messagebox.showinfo("Combine Ready", report_message + "\nReady to save the combined file.")

        save_path = filedialog.asksaveasfilename(
            title="Save Combined Empire File As...",
            defaultextension=".json", filetypes=[("JSON files", "*.json")],
            initialfile="combined_empire.json"
        )
        if not save_path: return

        try:
            with open(save_path, 'w', encoding='utf-8') as f:
                json.dump(final_combined_data_to_save, f, indent=4)
            
            if messagebox.askyesno("Combine Success", f"Combined data saved successfully to:\n{save_path}\n\nLoad this new file into the editor?"):
                self.other_top_level_keys = final_other_keys
                self.data["dealers"] = combined_dealers_list
                self.file_path = save_path
                self.root.title(f"Empire Editor - {os.path.basename(self.file_path)}")
                self.current_dealer_index = -1
                self._load_all_data_to_ui()
        except Exception as e:
            messagebox.showerror("Save Error", f"An error occurred while saving the combined file: {e}")

    def merge_dealers(self):
        if not self._sync_all_ui_to_data():
             messagebox.showerror("Merge Error", "Could not save current changes. Merging aborted.")
             return
        
        path_to_merge = filedialog.askopenfilename(
            title="Select Empire JSON File to Merge From",
            defaultextension=".json", filetypes=[("JSON files", "*.json")]
        )
        if not path_to_merge: return

        try:
            with open(path_to_merge, 'r', encoding='utf-8') as f:
                merge_file_content = json.load(f)
            
            if not isinstance(merge_file_content, dict) or not isinstance(merge_file_content.get("dealers"), list):
                messagebox.showerror("Merge Error", "Invalid merge file format. Expected an object with a 'dealers' list.")
                return
            dealers_to_merge_from_file = merge_file_content["dealers"]
        except Exception as e:
            messagebox.showerror("Merge Error", f"Error reading merge file: {e}"); return

        existing_dealers_map = {d.get('name'): i for i, d in enumerate(self.data["dealers"]) if d.get('name')}
        added_count, overwritten_count, kept_count, merge_cancelled_by_user = 0,0,0,False

        for new_dealer_obj in dealers_to_merge_from_file:
            new_dealer_name = new_dealer_obj.get('name')
            if not new_dealer_name:
                print(f"Warning: Skipping dealer with no name from merge file: {new_dealer_obj}")
                continue

            if new_dealer_name in existing_dealers_map:
                dialog = MergeConflictDialog(self.root, "Merge Conflict", new_dealer_name)
                action = dialog.result
                
                if action == "overwrite":
                    self.data["dealers"][existing_dealers_map[new_dealer_name]] = new_dealer_obj
                    overwritten_count += 1
                elif action == "keep":
                    kept_count += 1
                else:
                     messagebox.showinfo("Merge Cancelled", "Merge operation was cancelled by the user.")
                     merge_cancelled_by_user = True
                     break
            else:
                self.data["dealers"].append(new_dealer_obj)
                added_count += 1
                existing_dealers_map[new_dealer_name] = len(self.data["dealers"]) - 1

        if not merge_cancelled_by_user:
             messagebox.showinfo("Merge Complete", f"Merge operation finished.\nDealers Added: {added_count}\nDealers Overwritten: {overwritten_count}\nDealers Kept Existing: {kept_count}")
             self.update_dealer_listbox()
             self.clear_dealer_details()

    # --- Dealer List Management & Reordering ---
    def move_dealer_up(self):
        selected_indices = self.dealer_list.curselection()
        if not selected_indices:
            messagebox.showwarning("Move Error", "No dealer selected to move.")
            return
        
        current_pos = selected_indices[0]
        if current_pos == 0: return

        dealers_list = self.data.get("dealers", [])
        dealers_list[current_pos], dealers_list[current_pos - 1] = dealers_list[current_pos - 1], dealers_list[current_pos]
        
        if self.current_dealer_index == current_pos: self.current_dealer_index = current_pos - 1
        elif self.current_dealer_index == current_pos -1: self.current_dealer_index = current_pos

        self.update_dealer_listbox(select_index=current_pos - 1)

    def move_dealer_down(self):
        selected_indices = self.dealer_list.curselection()
        if not selected_indices:
            messagebox.showwarning("Move Error", "No dealer selected to move.")
            return

        current_pos = selected_indices[0]
        dealers_list = self.data.get("dealers", [])
        if current_pos == len(dealers_list) - 1: return

        dealers_list[current_pos], dealers_list[current_pos + 1] = dealers_list[current_pos + 1], dealers_list[current_pos]

        if self.current_dealer_index == current_pos: self.current_dealer_index = current_pos + 1
        elif self.current_dealer_index == current_pos + 1: self.current_dealer_index = current_pos

        self.update_dealer_listbox(select_index=current_pos + 1)

    # --- CRUD Operations for Dealers and Sub-Items ---
    def add_dealer(self):
        new_dealer = {
            "name": "New Dealer", "image": "", "tier": 0,
            "unlockRequirements": [], "deals": [], "dealDays": [],
            "curfewDeal": False,
            "repLogBase": 10,
            "gift": {"cost": 0, "rep": 0},
            "reward": {"rep_cost": 0, "unlockRep": 0, "type": "", "args": []},
            "drugs": [], "shipping": [],
            "dialogue": {key: [] for key in ["intro", "dealStart", "accept", "incomplete", "expire", "fail", "success", "reward"]}
        }
        self.data["dealers"].append(new_dealer)
        new_idx = len(self.data["dealers"]) - 1
        self.update_dealer_listbox(select_index=new_idx)
        self.load_selected_dealer(None)

    def remove_dealer(self):
        selected_indices = self.dealer_list.curselection()
        if not selected_indices:
            messagebox.showwarning("Remove Error", "No dealer selected to remove."); return
        
        index_to_remove = selected_indices[0]
        if 0 <= index_to_remove < len(self.data["dealers"]):
            dealer_name = self.data["dealers"][index_to_remove].get('name', 'Unnamed Dealer')
            if messagebox.askyesno("Confirm Removal", f"Are you sure you want to remove dealer '{dealer_name}'? This action cannot be undone."):
                del self.data["dealers"][index_to_remove]
                self.current_dealer_index = -1
                self.update_dealer_listbox()
                if not self.data["dealers"]: self.clear_dealer_details()
        else:
            messagebox.showerror("Remove Error", "Invalid dealer index selected for removal.")

    def load_selected_dealer(self, event):
        selected_indices = self.dealer_list.curselection()
        if not selected_indices:
            if self.current_dealer_index != -1:
                self.clear_dealer_details()
                self.current_dealer_index = -1
            return

        new_index = selected_indices[0]

        if self.current_dealer_index != -1 and self.current_dealer_index != new_index:
            if 0 <= self.current_dealer_index < len(self.data['dealers']):
                self._save_dealer_data(self.current_dealer_index, show_success=False)

        self.current_dealer_index = new_index
        try:
            if not (0 <= self.current_dealer_index < len(self.data['dealers'])):
                 raise IndexError("Selected dealer index is out of bounds.")
            
            dealer = self.data["dealers"][self.current_dealer_index]
            self.set_widget_state(self.details_scrollable_frame, 'normal')

            self.clear_entry(self.name_entry); self.name_entry.insert(0, dealer.get("name", ""))
            self.clear_entry(self.image_entry); self.image_entry.insert(0, dealer.get("image", ""))
            self.clear_entry(self.tier_entry); self.tier_entry.insert(0, str(dealer.get("tier", 0)))
            
            self.text_from_list_of_json_objects(self.unlock_requirements_text, dealer.get("unlockRequirements", []))
            
            self.clear_entry(self.rep_log_base_entry); self.rep_log_base_entry.insert(0, str(dealer.get("repLogBase", 10)))
            self.clear_entry(self.deal_days_entry); self.deal_days_entry.insert(0, self.string_from_list(dealer.get("dealDays", [])))
            self.curfew_deal_combobox.set("True" if dealer.get("curfewDeal", False) else "False")

            gift_data = dealer.get("gift", {})
            self.clear_entry(self.gift_cost_entry); self.gift_cost_entry.insert(0, str(gift_data.get("cost", 0)))
            self.clear_entry(self.gift_rep_entry); self.gift_rep_entry.insert(0, str(gift_data.get("rep", 0)))

            reward_data = dealer.get("reward", {})
            self.clear_entry(self.reward_rep_cost_entry); self.reward_rep_cost_entry.insert(0, str(reward_data.get("rep_cost", 0)))
            self.clear_entry(self.reward_unlock_rep_entry); self.reward_unlock_rep_entry.insert(0, str(reward_data.get("unlockRep", 0)))
            self.clear_entry(self.reward_type_entry); self.reward_type_entry.insert(0, reward_data.get("type", ""))
            self.clear_entry(self.reward_args_entry); self.reward_args_entry.insert(0, self.string_from_list(reward_data.get("args", [])))

            self.text_from_deals(self.deals_text, dealer.get("deals", []))

            self.update_drugs_listbox(); self.clear_drug_details()
            self.update_shipping_listbox(); self.clear_shipping_details()
            
            dialogue_data = dealer.get("dialogue", {})
            for field, widget in self.dialogue_text_widgets.items():
                self.text_from_list(widget, dialogue_data.get(field, []))

        except IndexError:
            messagebox.showerror("Load Error", "Selected dealer index is out of range.")
            self.clear_dealer_details(); self.current_dealer_index = -1
        except Exception as e:
            messagebox.showerror("Load Error", f"Failed to load dealer details: {e}\n{traceback.format_exc()}")
            self.clear_dealer_details(); self.current_dealer_index = -1

    def save_current_dealer(self):
        if self.current_dealer_index == -1:
            messagebox.showwarning("Save Error", "No dealer selected to save.")
            return
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])):
             messagebox.showerror("Save Error", "Selected dealer index is invalid. Cannot save.")
             return
        
        if self._save_dealer_data(self.current_dealer_index, show_success=True):
             self.update_dealer_listbox(select_index=self.current_dealer_index)

    def _save_dealer_data(self, index, show_success=True):
         try:
            if not (0 <= index < len(self.data['dealers'])):
                raise IndexError("Dealer index out of bounds during save operation.")
            
            dealer = self.data["dealers"][index]

            dealer["name"] = self.name_entry.get()
            dealer["image"] = self.image_entry.get()
            dealer["tier"] = self.safe_int(self.tier_entry.get(), 0)
            
            parsed_unlock_reqs = self.list_of_json_objects_from_text(self.unlock_requirements_text)
            if parsed_unlock_reqs is None:
                if show_success: messagebox.showerror("Save Error", "Invalid JSON in Unlock Requirements. Dealer not fully saved.")
                return False
            dealer["unlockRequirements"] = parsed_unlock_reqs
            
            dealer["repLogBase"] = self.safe_int(self.rep_log_base_entry.get(), 10)
            dealer["dealDays"] = self.list_from_string(self.deal_days_entry.get(), str)
            dealer["curfewDeal"] = self.curfew_deal_var.get() == "True"

            dealer["gift"] = {"cost": self.safe_int(self.gift_cost_entry.get()), "rep": self.safe_int(self.gift_rep_entry.get())}
            dealer["reward"] = {"rep_cost": self.safe_int(self.reward_rep_cost_entry.get()),"unlockRep": self.safe_int(self.reward_unlock_rep_entry.get()),"type": self.reward_type_entry.get(),"args": self.list_from_string(self.reward_args_entry.get(), str)}

            parsed_deals = self.deals_from_text(self.deals_text)
            if parsed_deals is None:
                if show_success: messagebox.showerror("Save Error", "Invalid format in 'Deals' section. Dealer not fully saved.")
                return False
            dealer["deals"] = parsed_deals

            if self.current_drug_index != -1:
                 if not self._save_drug_data(self.current_drug_index, show_success=False):
                     if show_success: messagebox.showerror("Save Error", "Failed to save current drug details. Dealer changes partially saved."); return False
            
            if self.current_shipping_index != -1:
                 if not self._save_shipping_data(self.current_shipping_index, show_success=False):
                     if show_success: messagebox.showerror("Save Error", "Failed to save current shipping details. Dealer changes partially saved."); return False
            
            dialogue_data = {}
            for field, widget in self.dialogue_text_widgets.items():
                dialogue_data[field] = self.list_from_text(widget, str)
            dealer["dialogue"] = dialogue_data

            if show_success:
                messagebox.showinfo("Save Success", f"Dealer '{dealer.get('name', 'N/A')}' saved successfully.")
            return True

         except IndexError:
            if show_success: messagebox.showerror("Save Error", "Selected dealer index is out of range during save."); return False
         except Exception as e:
            if show_success: messagebox.showerror("Save Error", f"An error occurred while saving dealer: {e}\n{traceback.format_exc()}"); return False

    def clear_dealer_details(self):
        self.clear_entry(self.name_entry); self.clear_entry(self.image_entry); self.clear_entry(self.tier_entry)
        self.clear_text(self.unlock_requirements_text)
        self.clear_entry(self.rep_log_base_entry); self.clear_entry(self.deal_days_entry)
        self.curfew_deal_combobox.set("False")
        
        self.clear_entry(self.gift_cost_entry); self.clear_entry(self.gift_rep_entry)
        self.clear_entry(self.reward_rep_cost_entry); self.clear_entry(self.reward_unlock_rep_entry)
        self.clear_entry(self.reward_type_entry); self.clear_entry(self.reward_args_entry)
        
        self.clear_text(self.deals_text)

        self.clear_listbox(self.drugs_list); self.clear_drug_details()
        self.clear_listbox(self.shipping_list); self.clear_shipping_details()
        
        for widget in self.dialogue_text_widgets.values(): self.clear_text(widget)

        self.set_widget_state(self.details_scrollable_frame, 'disabled')
        
        self.current_dealer_index = -1; self.current_drug_index = -1
        self.current_quality_index = -1; self.current_effect_index = -1; self.current_shipping_index = -1

    # --- Drug Section Management ---
    def update_drugs_listbox(self):
        sel_indices = self.drugs_list.curselection()
        current_sel_idx = sel_indices[0] if sel_indices else None
        self.clear_listbox(self.drugs_list); self.current_drug_index = -1

        if self.current_dealer_index != -1 and 0 <= self.current_dealer_index < len(self.data['dealers']):
            try:
                dealer = self.data["dealers"][self.current_dealer_index]
                for i, drug in enumerate(dealer.get("drugs", [])):
                    self.drugs_list.insert(tk.END, f"{i}: {drug.get('type', 'Unnamed Drug')}")
                
                if current_sel_idx is not None and 0 <= current_sel_idx < self.drugs_list.size():
                    self.drugs_list.selection_set(current_sel_idx)
                    self.drugs_list.activate(current_sel_idx); self.drugs_list.see(current_sel_idx)
            except (IndexError, KeyError): self.clear_drug_details()
        else: self.clear_drug_details()

    def add_drug(self):
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            messagebox.showwarning("Add Drug Error", "Please select a valid dealer first."); return
        new_drug = {"type": "New Drug", "unlockRep": 0, "base_dollar": 0, "base_rep": 0, "base_xp": 0, "rep_mult": 1.0, "xp_mult": 1.0, "qualities": [], "effects": []}
        dealer = self.data["dealers"][self.current_dealer_index]
        if not isinstance(dealer.get("drugs"), list): dealer["drugs"] = []
        dealer["drugs"].append(new_drug)
        new_idx = len(dealer["drugs"]) - 1
        self.update_drugs_listbox(); self.drugs_list.selection_clear(0, tk.END)
        self.drugs_list.selection_set(new_idx); self.drugs_list.activate(new_idx); self.drugs_list.see(new_idx)
        self.load_selected_drug(None)

    def remove_drug(self):
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            messagebox.showwarning("Remove Drug Error", "Please select a valid dealer first."); return
        selected_indices = self.drugs_list.curselection()
        if not selected_indices: messagebox.showwarning("Remove Drug Error", "No drug selected to remove."); return
        drug_idx_to_remove = selected_indices[0]
        dealer = self.data["dealers"][self.current_dealer_index]
        if isinstance(dealer.get("drugs"), list) and 0 <= drug_idx_to_remove < len(dealer["drugs"]):
            drug_name = dealer["drugs"][drug_idx_to_remove].get('type', 'Unnamed Drug')
            if messagebox.askyesno("Confirm Removal", f"Are you sure you want to remove drug '{drug_name}'?"):
                del dealer["drugs"][drug_idx_to_remove]
                self.current_drug_index = -1
                self.update_drugs_listbox(); self.clear_drug_details()
        else: messagebox.showerror("Remove Drug Error", "Invalid drug index or data structure problem.")

    def load_selected_drug(self, event):
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            self.clear_drug_details(); return
        selected_indices = self.drugs_list.curselection()
        if not selected_indices:
            if self.current_drug_index != -1: self.clear_drug_details(); self.current_drug_index = -1
            return
        new_drug_idx = selected_indices[0]
        if self.current_drug_index != -1 and self.current_drug_index != new_drug_idx:
            try:
                if 0 <= self.current_drug_index < len(self.data['dealers'][self.current_dealer_index].get('drugs',[])):
                    self._save_drug_data(self.current_drug_index, show_success=False)
            except (IndexError, KeyError): pass
        self.current_drug_index = new_drug_idx
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('drugs'), list) or not (0 <= self.current_drug_index < len(dealer['drugs'])):
                 raise IndexError("Selected drug index is out of bounds for the current dealer.")
            drug = dealer["drugs"][self.current_drug_index]
            self.set_widget_state(self.drug_details_frame, 'normal')
            self.clear_entry(self.drug_type_entry); self.drug_type_entry.insert(0, drug.get("type", ""))
            self.clear_entry(self.drug_unlock_rep_entry); self.drug_unlock_rep_entry.insert(0, str(drug.get("unlockRep", 0)))
            self.clear_entry(self.drug_base_dollar_entry); self.drug_base_dollar_entry.insert(0, str(drug.get("base_dollar", 0)))
            self.clear_entry(self.drug_base_rep_entry); self.drug_base_rep_entry.insert(0, str(drug.get("base_rep", 0)))
            self.clear_entry(self.drug_base_xp_entry); self.drug_base_xp_entry.insert(0, str(drug.get("base_xp", 0)))
            self.clear_entry(self.drug_rep_mult_entry); self.drug_rep_mult_entry.insert(0, str(drug.get("rep_mult", 1.0)))
            self.clear_entry(self.drug_xp_mult_entry); self.drug_xp_mult_entry.insert(0, str(drug.get("xp_mult", 1.0)))
            self.update_qualities_listbox(); self.clear_quality_details()
            self.update_effects_listbox(); self.clear_effect_details()
        except (IndexError, KeyError) as e:
            messagebox.showerror("Load Drug Error", f"Failed to load drug details (data missing or incorrect): {e}");
            self.clear_drug_details(); self.current_drug_index = -1
        except Exception as e:
            messagebox.showerror("Load Drug Error", f"An unexpected error occurred while loading drug: {e}\n{traceback.format_exc()}");
            self.clear_drug_details(); self.current_drug_index = -1

    def _save_drug_data(self, drug_idx_to_save, show_success=True):
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            if show_success: messagebox.showerror("Save Drug Error", "Cannot save drug, no valid dealer selected."); return False
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('drugs'), list) or not (0 <= drug_idx_to_save < len(dealer['drugs'])):
                 raise IndexError("Drug index for saving is out of bounds.")
            drug = dealer["drugs"][drug_idx_to_save]
            drug.update({
                "type": self.drug_type_entry.get(), "unlockRep": self.safe_int(self.drug_unlock_rep_entry.get()),
                "base_dollar": self.safe_int(self.drug_base_dollar_entry.get()), "base_rep": self.safe_int(self.drug_base_rep_entry.get()),
                "base_xp": self.safe_int(self.drug_base_xp_entry.get()), "rep_mult": self.safe_float(self.drug_rep_mult_entry.get(), 1.0),
                "xp_mult": self.safe_float(self.drug_xp_mult_entry.get(), 1.0)
            })
            if self.current_quality_index != -1:
                if not self._save_quality_data(self.current_quality_index, False):
                    if show_success: messagebox.showwarning("Save Drug Warning", "Could not save current quality details. Drug data partially saved."); return False
            if self.current_effect_index != -1:
                if not self._save_effect_data(self.current_effect_index, False):
                    if show_success: messagebox.showwarning("Save Drug Warning", "Could not save current effect details. Drug data partially saved."); return False
            return True
        except Exception as e:
            if show_success: messagebox.showerror("Save Drug Error", f"An error occurred saving drug details: {e}\n{traceback.format_exc()}")
            return False

    def clear_drug_details(self):
        for entry_widget in [self.drug_type_entry, self.drug_unlock_rep_entry, self.drug_base_dollar_entry,
                             self.drug_base_rep_entry, self.drug_base_xp_entry, self.drug_rep_mult_entry,
                             self.drug_xp_mult_entry]:
            self.clear_entry(entry_widget)
        self.clear_listbox(self.qualities_list); self.clear_quality_details()
        self.clear_listbox(self.effects_list); self.clear_effect_details()
        self.set_widget_state(self.drug_details_frame, 'disabled')
        self.current_drug_index = -1; self.current_quality_index = -1; self.current_effect_index = -1

    # --- Quality Section Management ---
    def update_qualities_listbox(self):
        sel_indices = self.qualities_list.curselection()
        current_sel_idx = sel_indices[0] if sel_indices else None
        self.clear_listbox(self.qualities_list); self.current_quality_index = -1
        if self.current_dealer_index != -1 and self.current_drug_index != -1 and \
           0 <= self.current_dealer_index < len(self.data['dealers']):
            try:
                drug = self.data["dealers"][self.current_dealer_index]["drugs"][self.current_drug_index]
                for i, quality in enumerate(drug.get("qualities", [])):
                    self.qualities_list.insert(tk.END, f"{i}: {quality.get('type', 'Unnamed Quality')}")
                if current_sel_idx is not None and 0 <= current_sel_idx < self.qualities_list.size():
                    self.qualities_list.selection_set(current_sel_idx)
                    self.qualities_list.activate(current_sel_idx); self.qualities_list.see(current_sel_idx)
            except (IndexError, KeyError): self.clear_quality_details()
        else: self.clear_quality_details()

    def add_quality(self):
        if self.current_dealer_index == -1 or self.current_drug_index == -1 or \
           not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            messagebox.showwarning("Add Quality Error", "Please select a valid dealer and drug first.")
            return
        new_quality = {"type": "New Quality", "dollar_mult": 1.0, "unlockRep": 0}
        try:
            drug = self.data["dealers"][self.current_dealer_index]["drugs"][self.current_drug_index]
            if not isinstance(drug.get("qualities"), list): drug["qualities"] = []
            drug["qualities"].append(new_quality)
            new_idx = len(drug["qualities"]) - 1
            self.update_qualities_listbox()
            self.qualities_list.selection_clear(0, tk.END); self.qualities_list.selection_set(new_idx)
            self.qualities_list.activate(new_idx); self.qualities_list.see(new_idx)
            self.load_selected_quality(None)
        except (IndexError, KeyError) as e:
            messagebox.showerror("Add Quality Error", f"Could not add quality (check dealer/drug selection): {e}")

    def remove_quality(self):
        if self.current_dealer_index == -1 or self.current_drug_index == -1 or \
           not (0 <= self.current_dealer_index < len(self.data['dealers'])): return
        sel_indices = self.qualities_list.curselection()
        if not sel_indices: messagebox.showwarning("Remove Quality Error", "No quality selected."); return
        quality_idx_to_remove = sel_indices[0]
        try:
            drug = self.data["dealers"][self.current_dealer_index]["drugs"][self.current_drug_index]
            if not isinstance(drug.get('qualities'), list) or not (0 <= quality_idx_to_remove < len(drug['qualities'])):
                messagebox.showerror("Remove Quality Error", "Quality index out of bounds."); return
            quality_name = drug["qualities"][quality_idx_to_remove].get('type', 'Unnamed Quality')
            if messagebox.askyesno("Confirm Removal", f"Are you sure you want to remove quality '{quality_name}'?"):
                del drug["qualities"][quality_idx_to_remove]
                self.current_quality_index = -1
                self.update_qualities_listbox(); self.clear_quality_details()
        except (IndexError, KeyError) as e:
            messagebox.showerror("Remove Quality Error", f"Could not remove quality: {e}")

    def load_selected_quality(self, event):
        if self.current_dealer_index == -1 or self.current_drug_index == -1 or \
           not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            self.clear_quality_details(); return
        sel_indices = self.qualities_list.curselection()
        if not sel_indices:
            if self.current_quality_index != -1: self.clear_quality_details(); self.current_quality_index = -1
            return
        new_quality_idx = sel_indices[0]
        if self.current_quality_index != -1 and self.current_quality_index != new_quality_idx:
            try:
                if 0 <= self.current_quality_index < len(self.data['dealers'][self.current_dealer_index]['drugs'][self.current_drug_index].get('qualities',[])):
                    self._save_quality_data(self.current_quality_index, False)
            except (IndexError, KeyError): pass
        self.current_quality_index = new_quality_idx
        try:
            quality = self.data["dealers"][self.current_dealer_index]["drugs"][self.current_drug_index]["qualities"][self.current_quality_index]
            self.set_widget_state(self.quality_details_frame, 'normal')
            self.clear_entry(self.quality_type_entry); self.quality_type_entry.insert(0, quality.get("type", ""))
            self.clear_entry(self.quality_dollar_mult_entry); self.quality_dollar_mult_entry.insert(0, str(quality.get("dollar_mult", 1.0)))
            self.clear_entry(self.quality_unlock_rep_entry); self.quality_unlock_rep_entry.insert(0, str(quality.get("unlockRep", 0)))
        except (IndexError, KeyError) as e:
            messagebox.showerror("Load Quality Error", f"Failed to load quality details: {e}");
            self.clear_quality_details(); self.current_quality_index = -1
        except Exception as e:
            messagebox.showerror("Load Quality Error", f"An unexpected error occurred: {e}\n{traceback.format_exc()}");
            self.clear_quality_details(); self.current_quality_index = -1

    def _save_quality_data(self, quality_idx_to_save, show_success=True):
        if self.current_dealer_index == -1 or self.current_drug_index == -1 or \
           not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            if show_success: messagebox.showerror("Save Quality Error", "Cannot save quality, no valid dealer/drug selected."); return False
        try:
            drug = self.data["dealers"][self.current_dealer_index]["drugs"][self.current_drug_index]
            if not isinstance(drug.get('qualities'), list) or not (0 <= quality_idx_to_save < len(drug['qualities'])):
                 raise IndexError("Quality index for saving is out of bounds.")
            quality = drug["qualities"][quality_idx_to_save]
            quality.update({
                "type": self.quality_type_entry.get(),
                "dollar_mult": self.safe_float(self.quality_dollar_mult_entry.get(), 1.0),
                "unlockRep": self.safe_int(self.quality_unlock_rep_entry.get())
            })
            return True
        except Exception as e:
            if show_success: messagebox.showerror("Save Quality Error", f"An error occurred saving quality: {e}\n{traceback.format_exc()}")
            return False

    def clear_quality_details(self):
        for entry_widget in [self.quality_type_entry, self.quality_dollar_mult_entry, self.quality_unlock_rep_entry]:
            self.clear_entry(entry_widget)
        self.set_widget_state(self.quality_details_frame, 'disabled')
        self.current_quality_index = -1

    # --- Effect Section Management ---
    def update_effects_listbox(self):
        sel_indices = self.effects_list.curselection()
        current_sel_idx = sel_indices[0] if sel_indices else None
        self.clear_listbox(self.effects_list); self.current_effect_index = -1
        if self.current_dealer_index != -1 and self.current_drug_index != -1 and \
           0 <= self.current_dealer_index < len(self.data['dealers']):
            try:
                drug = self.data["dealers"][self.current_dealer_index]["drugs"][self.current_drug_index]
                for i, effect in enumerate(drug.get("effects", [])):
                    self.effects_list.insert(tk.END, f"{i}: {effect.get('name', 'Unnamed Effect')}")
                if current_sel_idx is not None and 0 <= current_sel_idx < self.effects_list.size():
                    self.effects_list.selection_set(current_sel_idx)
                    self.effects_list.activate(current_sel_idx); self.effects_list.see(current_sel_idx)
            except (IndexError, KeyError): self.clear_effect_details()
        else: self.clear_effect_details()

    def add_effect(self):
        if self.current_dealer_index == -1 or self.current_drug_index == -1 or \
           not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            messagebox.showwarning("Add Effect Error", "Please select a valid dealer and drug first.")
            return
        new_effect = {"name": "New Effect", "unlockRep": 0, "probability": 0.0, "dollar_mult": 1.0}
        try:
            drug = self.data["dealers"][self.current_dealer_index]["drugs"][self.current_drug_index]
            if not isinstance(drug.get("effects"), list): drug["effects"] = []
            drug["effects"].append(new_effect)
            new_idx = len(drug["effects"]) - 1
            self.update_effects_listbox()
            self.effects_list.selection_clear(0, tk.END); self.effects_list.selection_set(new_idx)
            self.effects_list.activate(new_idx); self.effects_list.see(new_idx)
            self.load_selected_effect(None)
        except (IndexError, KeyError) as e:
            messagebox.showerror("Add Effect Error", f"Could not add effect: {e}")

    def remove_effect(self):
        if self.current_dealer_index == -1 or self.current_drug_index == -1 or \
           not (0 <= self.current_dealer_index < len(self.data['dealers'])): return
        sel_indices = self.effects_list.curselection()
        if not sel_indices: messagebox.showwarning("Remove Effect Error", "No effect selected."); return
        effect_idx_to_remove = sel_indices[0]
        try:
            drug = self.data["dealers"][self.current_dealer_index]["drugs"][self.current_drug_index]
            if not isinstance(drug.get('effects'), list) or not (0 <= effect_idx_to_remove < len(drug['effects'])):
                messagebox.showerror("Remove Effect Error", "Effect index out of bounds."); return
            effect_name = drug["effects"][effect_idx_to_remove].get('name', 'Unnamed Effect')
            if messagebox.askyesno("Confirm Removal", f"Are you sure you want to remove effect '{effect_name}'?"):
                del drug["effects"][effect_idx_to_remove]
                self.current_effect_index = -1
                self.update_effects_listbox(); self.clear_effect_details()
        except (IndexError, KeyError) as e:
            messagebox.showerror("Remove Effect Error", f"Could not remove effect: {e}")

    def load_selected_effect(self, event):
        if self.current_dealer_index == -1 or self.current_drug_index == -1 or \
           not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            self.clear_effect_details(); return
        sel_indices = self.effects_list.curselection()
        if not sel_indices:
            if self.current_effect_index != -1: self.clear_effect_details(); self.current_effect_index = -1
            return
        new_effect_idx = sel_indices[0]
        if self.current_effect_index != -1 and self.current_effect_index != new_effect_idx:
            try:
                if 0 <= self.current_effect_index < len(self.data['dealers'][self.current_dealer_index]['drugs'][self.current_drug_index].get('effects',[])):
                    self._save_effect_data(self.current_effect_index, False)
            except (IndexError, KeyError): pass
        self.current_effect_index = new_effect_idx
        try:
            effect = self.data["dealers"][self.current_dealer_index]["drugs"][self.current_drug_index]["effects"][self.current_effect_index]
            self.set_widget_state(self.effect_details_frame, 'normal')
            self.clear_entry(self.effect_name_entry); self.effect_name_entry.insert(0, effect.get("name", ""))
            self.clear_entry(self.effect_unlock_rep_entry); self.effect_unlock_rep_entry.insert(0, str(effect.get("unlockRep", 0)))
            self.clear_entry(self.effect_probability_entry); self.effect_probability_entry.insert(0, str(effect.get("probability", 0.0)))
            self.clear_entry(self.effect_dollar_mult_entry); self.effect_dollar_mult_entry.insert(0, str(effect.get("dollar_mult", 1.0)))
        except (IndexError, KeyError) as e:
            messagebox.showerror("Load Effect Error", f"Failed to load effect details: {e}");
            self.clear_effect_details(); self.current_effect_index = -1
        except Exception as e:
            messagebox.showerror("Load Effect Error", f"An unexpected error occurred: {e}\n{traceback.format_exc()}");
            self.clear_effect_details(); self.current_effect_index = -1

    def _save_effect_data(self, effect_idx_to_save, show_success=True):
        if self.current_dealer_index == -1 or self.current_drug_index == -1 or \
           not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            if show_success: messagebox.showerror("Save Effect Error", "Cannot save effect, no valid dealer/drug selected."); return False
        try:
            drug = self.data["dealers"][self.current_dealer_index]["drugs"][self.current_drug_index]
            if not isinstance(drug.get('effects'), list) or not (0 <= effect_idx_to_save < len(drug['effects'])):
                 raise IndexError("Effect index for saving is out of bounds.")
            effect = drug["effects"][effect_idx_to_save]
            effect.update({
                "name": self.effect_name_entry.get(),
                "unlockRep": self.safe_int(self.effect_unlock_rep_entry.get()),
                "probability": self.safe_float(self.effect_probability_entry.get()),
                "dollar_mult": self.safe_float(self.effect_dollar_mult_entry.get(), 1.0)
            })
            return True
        except Exception as e:
            if show_success: messagebox.showerror("Save Effect Error", f"An error occurred saving effect: {e}\n{traceback.format_exc()}")
            return False

    def clear_effect_details(self):
        for entry_widget in [self.effect_name_entry, self.effect_unlock_rep_entry,
                             self.effect_probability_entry, self.effect_dollar_mult_entry]:
            self.clear_entry(entry_widget)
        self.set_widget_state(self.effect_details_frame, 'disabled')
        self.current_effect_index = -1

    # --- Shipping Section Management ---
    def update_shipping_listbox(self):
        sel_indices = self.shipping_list.curselection()
        current_sel_idx = sel_indices[0] if sel_indices else None
        self.clear_listbox(self.shipping_list); self.current_shipping_index = -1
        if self.current_dealer_index != -1 and 0 <= self.current_dealer_index < len(self.data['dealers']):
            try:
                dealer = self.data["dealers"][self.current_dealer_index]
                for i, shipping_item in enumerate(dealer.get("shipping", [])):
                    self.shipping_list.insert(tk.END, f"{i}: {shipping_item.get('name', 'Unnamed Shipping')}")
                if current_sel_idx is not None and 0 <= current_sel_idx < self.shipping_list.size():
                    self.shipping_list.selection_set(current_sel_idx)
                    self.shipping_list.activate(current_sel_idx); self.shipping_list.see(current_sel_idx)
            except (IndexError, KeyError): self.clear_shipping_details()
        else: self.clear_shipping_details()

    def add_shipping(self):
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            messagebox.showwarning("Add Shipping Error", "Please select a valid dealer first.")
            return
        new_shipping = {"name": "New Shipping", "cost": 0, "unlockRep": 0, "minAmount": 0, "stepAmount": 1, "maxAmount": 100, "dealModifier": [0.0,0.0,0.0,0.0]}
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get("shipping"), list): dealer["shipping"] = []
            dealer["shipping"].append(new_shipping)
            new_idx = len(dealer["shipping"]) - 1
            self.update_shipping_listbox()
            self.shipping_list.selection_clear(0, tk.END); self.shipping_list.selection_set(new_idx)
            self.shipping_list.activate(new_idx); self.shipping_list.see(new_idx)
            self.load_selected_shipping(None)
        except (IndexError, KeyError) as e:
            messagebox.showerror("Add Shipping Error", f"Could not add shipping option: {e}")

    def remove_shipping(self):
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])): return
        sel_indices = self.shipping_list.curselection()
        if not sel_indices: messagebox.showwarning("Remove Shipping Error", "No shipping option selected."); return
        shipping_idx_to_remove = sel_indices[0]
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('shipping'), list) or not (0 <= shipping_idx_to_remove < len(dealer['shipping'])):
                messagebox.showerror("Remove Shipping Error", "Shipping index out of bounds."); return
            shipping_name = dealer["shipping"][shipping_idx_to_remove].get('name', 'Unnamed Shipping')
            if messagebox.askyesno("Confirm Removal", f"Are you sure you want to remove shipping option '{shipping_name}'?"):
                del dealer["shipping"][shipping_idx_to_remove]
                self.current_shipping_index = -1
                self.update_shipping_listbox(); self.clear_shipping_details()
        except (IndexError, KeyError) as e:
            messagebox.showerror("Remove Shipping Error", f"Could not remove shipping option: {e}")

    def load_selected_shipping(self, event):
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            self.clear_shipping_details(); return
        sel_indices = self.shipping_list.curselection()
        if not sel_indices:
            if self.current_shipping_index != -1: self.clear_shipping_details(); self.current_shipping_index = -1
            return
        new_shipping_idx = sel_indices[0]
        if self.current_shipping_index != -1 and self.current_shipping_index != new_shipping_idx:
            try:
                if 0 <= self.current_shipping_index < len(self.data['dealers'][self.current_dealer_index].get('shipping',[])):
                    self._save_shipping_data(self.current_shipping_index, False)
            except (IndexError, KeyError): pass
        self.current_shipping_index = new_shipping_idx
        try:
            shipping_item = self.data["dealers"][self.current_dealer_index]["shipping"][self.current_shipping_index]
            self.set_widget_state(self.shipping_details_frame, 'normal')
            self.clear_entry(self.shipping_name_entry); self.shipping_name_entry.insert(0, shipping_item.get("name", ""))
            self.clear_entry(self.shipping_cost_entry); self.shipping_cost_entry.insert(0, str(shipping_item.get("cost", 0)))
            self.clear_entry(self.shipping_unlock_rep_entry); self.shipping_unlock_rep_entry.insert(0, str(shipping_item.get("unlockRep", 0)))
            self.clear_entry(self.shipping_min_amount_entry); self.shipping_min_amount_entry.insert(0, str(shipping_item.get("minAmount", 0)))
            self.clear_entry(self.shipping_step_amount_entry); self.shipping_step_amount_entry.insert(0, str(shipping_item.get("stepAmount", 1)))
            self.clear_entry(self.shipping_max_amount_entry); self.shipping_max_amount_entry.insert(0, str(shipping_item.get("maxAmount", 100)))
            self.clear_entry(self.shipping_deal_modifier_entry); self.shipping_deal_modifier_entry.insert(0, self.string_from_list(shipping_item.get("dealModifier", [0.0,0.0,0.0,0.0])))
        except (IndexError, KeyError) as e:
            messagebox.showerror("Load Shipping Error", f"Failed to load shipping details: {e}");
            self.clear_shipping_details(); self.current_shipping_index = -1
        except Exception as e:
            messagebox.showerror("Load Shipping Error", f"An unexpected error occurred: {e}\n{traceback.format_exc()}");
            self.clear_shipping_details(); self.current_shipping_index = -1

    def _save_shipping_data(self, shipping_idx_to_save, show_success=True):
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            if show_success: messagebox.showerror("Save Shipping Error", "Cannot save shipping, no valid dealer selected."); return False
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('shipping'), list) or not (0 <= shipping_idx_to_save < len(dealer['shipping'])):
                 raise IndexError("Shipping index for saving is out of bounds.")
            shipping_item = dealer["shipping"][shipping_idx_to_save]
            shipping_item.update({
                "name": self.shipping_name_entry.get(), "cost": self.safe_int(self.shipping_cost_entry.get()),
                "unlockRep": self.safe_int(self.shipping_unlock_rep_entry.get()), "minAmount": self.safe_int(self.shipping_min_amount_entry.get()),
                "stepAmount": self.safe_int(self.shipping_step_amount_entry.get(), 1), "maxAmount": self.safe_int(self.shipping_max_amount_entry.get(), 100)
            })
            dm_list = self.list_from_string(self.shipping_deal_modifier_entry.get(), float)
            if len(dm_list) == 4: shipping_item["dealModifier"] = dm_list
            else:
                shipping_item["dealModifier"] = shipping_item.get("dealModifier", [0.0,0.0,0.0,0.0])
                if show_success:
                    messagebox.showwarning("Save Shipping Warning", "Deal Modifier for shipping option was not 4 numbers separated by commas. Its value was not updated or reverted to default.")
            return True
        except Exception as e:
            if show_success: messagebox.showerror("Save Shipping Error", f"An error occurred saving shipping option: {e}\n{traceback.format_exc()}")
            return False

    def clear_shipping_details(self):
        for entry_widget in [self.shipping_name_entry, self.shipping_cost_entry, self.shipping_unlock_rep_entry,
                             self.shipping_min_amount_entry, self.shipping_step_amount_entry,
                             self.shipping_max_amount_entry, self.shipping_deal_modifier_entry]:
            self.clear_entry(entry_widget)
        self.set_widget_state(self.shipping_details_frame, 'disabled')
        self.current_shipping_index = -1


if __name__ == "__main__":
    root = tk.Tk()
    style = ttk.Style(root)
    try:
        if os.name == 'nt': style.theme_use('vista')
        elif os.name == 'posix':
             available_themes = style.theme_names()
             if 'clam' in available_themes: style.theme_use('clam')
             elif 'aqua' in available_themes: style.theme_use('aqua')
    except tk.TclError:
        print("A selected ttk theme was not found, using default system theme.")

    app = DealerEditorApp(root)
    root.mainloop()
