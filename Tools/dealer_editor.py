import json
import tkinter as tk
from tkinter import ttk, filedialog, messagebox, simpledialog
import os # Needed for path operations
import re # Needed for filename sanitization

# --- Custom Dialog for Merge Conflicts ---
class MergeConflictDialog(simpledialog.Dialog):
    """Custom dialog to resolve merge conflicts for dealers."""
    def __init__(self, parent, title, dealer_name):
        self.dealer_name = dealer_name
        self.result = "cancel" # Default action
        super().__init__(parent, title)

    def body(self, master):
        tk.Label(master, text=f"Dealer '{self.dealer_name}' already exists.").grid(row=0, columnspan=2, pady=10)
        tk.Label(master, text="Choose an action:").grid(row=1, columnspan=2)
        return None # focus_set() handled by simpledialog

    def buttonbox(self):
        box = tk.Frame(self)

        keep_button = ttk.Button(box, text="Keep Existing", width=15, command=self.keep_existing)
        keep_button.pack(side=tk.LEFT, padx=5, pady=5)
        overwrite_button = ttk.Button(box, text="Overwrite with New", width=15, command=self.overwrite)
        overwrite_button.pack(side=tk.LEFT, padx=5, pady=5)
        cancel_button = ttk.Button(box, text="Cancel Merge", width=15, command=self.cancel)
        cancel_button.pack(side=tk.LEFT, padx=5, pady=5)

        self.bind("<Return>", lambda event: self.keep_existing()) # Default to keep
        self.bind("<Escape>", self.cancel)

        box.pack()

    def keep_existing(self, event=None):
        self.result = "keep"
        self.ok() # Close the dialog

    def overwrite(self, event=None):
        self.result = "overwrite"
        self.ok() # Close the dialog

    # cancel is handled by simpledialog.Dialog


class DealerEditorApp:
    """
    A Tkinter application for editing dealer data stored in a JSON file.
    Allows adding, removing, modifying, splitting, combining, and merging dealer data.
    Aligned with a specific JSON schema.
    """
    def __init__(self, root):
        """
        Initializes the application.

        Args:
            root: The main Tkinter window.
        """
        self.root = root
        self.root.title("Dealer Editor")
        self.root.minsize(850, 650) # Increased min size slightly

        # Initialize data structure and tracking variables
        self.data = {"dealers": []}
        self.current_dealer_index = -1
        self.current_drug_index = -1
        self.current_quality_index = -1
        self.current_effect_index = -1
        self.current_shipping_index = -1
        self.file_path = None # To keep track of the currently open file

        # Build the UI
        self.create_widgets()
        # Load initial empty state for details
        self.clear_dealer_details()

    def create_widgets(self):
        """Creates the main widgets and layout of the application."""
        # --- Menu ---
        menubar = tk.Menu(self.root)
        filemenu = tk.Menu(menubar, tearoff=0)
        filemenu.add_command(label="New", command=self.new_file)
        filemenu.add_command(label="Open...", command=self.load_json)
        filemenu.add_command(label="Save", command=self.save_json)
        filemenu.add_command(label="Save As...", command=self.save_json_as)
        filemenu.add_separator()
        filemenu.add_command(label="Split Dealers...", command=self.split_dealers)
        filemenu.add_command(label="Combine Dealers...", command=self.combine_dealers)
        filemenu.add_command(label="Merge Dealers...", command=self.merge_dealers)
        filemenu.add_separator()
        filemenu.add_command(label="Exit", command=self.root.quit)
        menubar.add_cascade(label="File", menu=filemenu)
        self.root.config(menu=menubar)

        # --- Main Paned Window (allows resizing) ---
        main_pane = ttk.PanedWindow(self.root, orient=tk.HORIZONTAL)
        main_pane.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        # --- Dealer List Frame ---
        dealers_frame = ttk.LabelFrame(main_pane, text="Dealers", padding=(10, 5))
        main_pane.add(dealers_frame, weight=1) # Add to paned window

        # Use a frame for the listbox and scrollbar
        dealer_list_frame = ttk.Frame(dealers_frame)
        dealer_list_frame.pack(fill=tk.BOTH, expand=True, pady=(0, 5))

        dealer_scrollbar = ttk.Scrollbar(dealer_list_frame, orient=tk.VERTICAL)
        self.dealer_list = tk.Listbox(dealer_list_frame, yscrollcommand=dealer_scrollbar.set, exportselection=False)
        dealer_scrollbar.config(command=self.dealer_list.yview)
        dealer_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.dealer_list.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        self.dealer_list.bind('<<ListboxSelect>>', self.load_selected_dealer)

        dealer_button_frame = ttk.Frame(dealers_frame)
        dealer_button_frame.pack(fill=tk.X, pady=5)
        add_dealer_button = ttk.Button(dealer_button_frame, text="Add Dealer", command=self.add_dealer)
        add_dealer_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(0, 2))
        remove_dealer_button = ttk.Button(dealer_button_frame, text="Remove Dealer", command=self.remove_dealer)
        remove_dealer_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(2, 0))

        # --- Details Frame (Scrollable) ---
        details_outer_frame = ttk.Frame(main_pane)
        main_pane.add(details_outer_frame, weight=3) # Add to paned window

        details_canvas = tk.Canvas(details_outer_frame)
        details_scrollbar = ttk.Scrollbar(details_outer_frame, orient="vertical", command=details_canvas.yview)
        # Make the details_frame scrollable
        self.details_scrollable_frame = ttk.Frame(details_canvas, padding=(10, 5))

        self.details_scrollable_frame.bind(
            "<Configure>",
            lambda e: details_canvas.configure(
                scrollregion=details_canvas.bbox("all")
            )
        )
        # Bind mouse wheel scrolling to the canvas
        # This works on Windows and MacOS
        details_canvas.bind_all("<MouseWheel>", lambda e: details_canvas.yview_scroll(int(-1*(e.delta/120)), "units"))
        # This works on Linux
        details_canvas.bind_all("<Button-4>", lambda e: details_canvas.yview_scroll(-1, "units"))
        details_canvas.bind_all("<Button-5>", lambda e: details_canvas.yview_scroll(1, "units"))


        details_canvas.create_window((0, 0), window=self.details_scrollable_frame, anchor="nw")
        details_canvas.configure(yscrollcommand=details_scrollbar.set)

        details_canvas.pack(side="left", fill="both", expand=True)
        details_scrollbar.pack(side="right", fill="y")

        # --- Dealer Details Widgets (inside scrollable frame) ---
        self.create_dealer_details_widgets(self.details_scrollable_frame)


    def create_dealer_details_widgets(self, parent_frame):
        """Creates widgets for editing the details of a selected dealer (Schema Aligned)."""
        current_row = 0

        # --- Basic Info ---
        basic_info_frame = ttk.LabelFrame(parent_frame, text="Basic Info", padding=(10, 5))
        basic_info_frame.grid(row=current_row, column=0, padx=5, pady=5, sticky="ew"); current_row += 1
        basic_info_frame.columnconfigure(1, weight=1) # Make entry expand

        ttk.Label(basic_info_frame, text="Name:").grid(row=0, column=0, padx=5, pady=2, sticky="w")
        self.name_entry = ttk.Entry(basic_info_frame)
        self.name_entry.grid(row=0, column=1, padx=5, pady=2, sticky="ew")

        ttk.Label(basic_info_frame, text="Image:").grid(row=1, column=0, padx=5, pady=2, sticky="w")
        self.image_entry = ttk.Entry(basic_info_frame)
        self.image_entry.grid(row=1, column=1, padx=5, pady=2, sticky="ew")

        ttk.Label(basic_info_frame, text="Deal Times (int, comma-sep):").grid(row=2, column=0, padx=5, pady=2, sticky="w")
        self.deal_times_entry = ttk.Entry(basic_info_frame)
        self.deal_times_entry.grid(row=2, column=1, padx=5, pady=2, sticky="ew")

        ttk.Label(basic_info_frame, text="Deal Times Mult (float, comma-sep):").grid(row=3, column=0, padx=5, pady=2, sticky="w")
        self.deal_times_mult_entry = ttk.Entry(basic_info_frame)
        self.deal_times_mult_entry.grid(row=3, column=1, padx=5, pady=2, sticky="ew")

        ttk.Label(basic_info_frame, text="Penalties (int, comma-sep):").grid(row=4, column=0, padx=5, pady=2, sticky="w")
        self.penalties_entry = ttk.Entry(basic_info_frame)
        self.penalties_entry.grid(row=4, column=1, padx=5, pady=2, sticky="ew")

        ttk.Label(basic_info_frame, text="Unlock Req. (str, comma-sep):").grid(row=5, column=0, padx=5, pady=2, sticky="w")
        self.unlock_requirements_entry = ttk.Entry(basic_info_frame)
        self.unlock_requirements_entry.grid(row=5, column=1, padx=5, pady=2, sticky="ew")

        # --- Drugs Section ---
        drugs_frame = ttk.LabelFrame(parent_frame, text="Drugs", padding=(10, 5))
        drugs_frame.grid(row=current_row, column=0, padx=5, pady=5, sticky="nsew"); current_row += 1
        drugs_frame.columnconfigure(0, weight=1) # Make listbox expand

        # Drug List
        drugs_list_frame = ttk.Frame(drugs_frame)
        drugs_list_frame.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 5))
        drugs_list_frame.columnconfigure(0, weight=1)
        drug_scrollbar = ttk.Scrollbar(drugs_list_frame, orient=tk.VERTICAL)
        self.drugs_list = tk.Listbox(drugs_list_frame, height=5, yscrollcommand=drug_scrollbar.set, exportselection=False)
        drug_scrollbar.config(command=self.drugs_list.yview)
        drug_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.drugs_list.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        self.drugs_list.bind('<<ListboxSelect>>', self.load_selected_drug)

        # Drug Buttons
        drug_button_frame = ttk.Frame(drugs_frame)
        drug_button_frame.grid(row=1, column=0, columnspan=2, sticky="ew", pady=2)
        add_drug_button = ttk.Button(drug_button_frame, text="Add Drug", command=self.add_drug)
        add_drug_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(0, 2))
        remove_drug_button = ttk.Button(drug_button_frame, text="Remove Drug", command=self.remove_drug)
        remove_drug_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(2, 0))

        # Drug Details
        self.drug_details_frame = ttk.LabelFrame(drugs_frame, text="Drug Details", padding=(10, 5))
        self.drug_details_frame.grid(row=2, column=0, columnspan=2, padx=5, pady=5, sticky="ew")
        self.create_drug_details_widgets(self.drug_details_frame)

        # --- Shipping Section ---
        shipping_frame = ttk.LabelFrame(parent_frame, text="Shipping", padding=(10, 5))
        shipping_frame.grid(row=current_row, column=0, padx=5, pady=5, sticky="nsew"); current_row += 1
        shipping_frame.columnconfigure(0, weight=1)

        # Shipping List
        shipping_list_frame = ttk.Frame(shipping_frame)
        shipping_list_frame.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 5))
        shipping_list_frame.columnconfigure(0, weight=1)
        shipping_scrollbar = ttk.Scrollbar(shipping_list_frame, orient=tk.VERTICAL)
        self.shipping_list = tk.Listbox(shipping_list_frame, height=5, yscrollcommand=shipping_scrollbar.set, exportselection=False)
        shipping_scrollbar.config(command=self.shipping_list.yview)
        shipping_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.shipping_list.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        self.shipping_list.bind('<<ListboxSelect>>', self.load_selected_shipping)

        # Shipping Buttons
        shipping_button_frame = ttk.Frame(shipping_frame)
        shipping_button_frame.grid(row=1, column=0, columnspan=2, sticky="ew", pady=2)
        add_shipping_button = ttk.Button(shipping_button_frame, text="Add Shipping", command=self.add_shipping)
        add_shipping_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(0, 2))
        remove_shipping_button = ttk.Button(shipping_button_frame, text="Remove Shipping", command=self.remove_shipping)
        remove_shipping_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(2, 0))

        # Shipping Details
        self.shipping_details_frame = ttk.LabelFrame(shipping_frame, text="Shipping Details", padding=(10, 5))
        self.shipping_details_frame.grid(row=2, column=0, columnspan=2, padx=5, pady=5, sticky="ew")
        self.create_shipping_details_widgets(self.shipping_details_frame)

        # --- Dialogue Section ---
        dialogue_frame = ttk.LabelFrame(parent_frame, text="Dialogue (One line per entry)", padding=(10, 5))
        dialogue_frame.grid(row=current_row, column=0, padx=5, pady=5, sticky="nsew"); current_row += 1
        # dialogue_frame.columnconfigure(1, weight=1) # Make entry expand - Handled below
        self.create_dialogue_details_widgets(dialogue_frame)

        # --- Save Button ---
        save_dealer_button = ttk.Button(parent_frame, text="Save Current Dealer", command=self.save_current_dealer)
        save_dealer_button.grid(row=current_row, column=0, pady=15, padx=5, sticky="ew"); current_row += 1

        # --- Configure parent frame column weights ---
        parent_frame.columnconfigure(0, weight=1)


    def create_drug_details_widgets(self, parent_frame):
        """Creates widgets for editing drug details (Schema Aligned)."""
        parent_frame.columnconfigure(1, weight=1) # Make entries expand
        current_row = 0

        # --- Drug Basic Info ---
        ttk.Label(parent_frame, text="Type (str):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.drug_type_entry = ttk.Entry(parent_frame)
        self.drug_type_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Bonus Dollar (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.drug_bonus_dollar_entry = ttk.Entry(parent_frame)
        self.drug_bonus_dollar_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Bonus Rep (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.drug_bonus_rep_entry = ttk.Entry(parent_frame)
        self.drug_bonus_rep_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Base Dollar Mult (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.drug_base_dollar_mult_entry = ttk.Entry(parent_frame)
        self.drug_base_dollar_mult_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Base Rep Mult (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.drug_base_rep_mult_entry = ttk.Entry(parent_frame)
        self.drug_base_rep_mult_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Unlock Rep (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.drug_unlock_rep_entry = ttk.Entry(parent_frame)
        self.drug_unlock_rep_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        # --- Qualities ---
        qualities_frame = ttk.LabelFrame(parent_frame, text="Qualities", padding=(10, 5))
        qualities_frame.grid(row=current_row, column=0, columnspan=2, padx=5, pady=5, sticky="ew"); current_row += 1
        qualities_frame.columnconfigure(0, weight=1)

        # Quality List
        qualities_list_frame = ttk.Frame(qualities_frame)
        qualities_list_frame.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 5))
        qualities_list_frame.columnconfigure(0, weight=1)
        quality_scrollbar = ttk.Scrollbar(qualities_list_frame, orient=tk.VERTICAL)
        self.qualities_list = tk.Listbox(qualities_list_frame, height=3, yscrollcommand=quality_scrollbar.set, exportselection=False)
        quality_scrollbar.config(command=self.qualities_list.yview)
        quality_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.qualities_list.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        self.qualities_list.bind('<<ListboxSelect>>', self.load_selected_quality)

        # Quality Buttons
        quality_button_frame = ttk.Frame(qualities_frame)
        quality_button_frame.grid(row=1, column=0, columnspan=2, sticky="ew", pady=2)
        add_quality_button = ttk.Button(quality_button_frame, text="Add Quality", command=self.add_quality)
        add_quality_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(0, 2))
        remove_quality_button = ttk.Button(quality_button_frame, text="Remove Quality", command=self.remove_quality)
        remove_quality_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(2, 0))

        # Quality Details
        self.quality_details_frame = ttk.LabelFrame(qualities_frame, text="Quality Details", padding=(10, 5))
        self.quality_details_frame.grid(row=2, column=0, columnspan=2, padx=5, pady=5, sticky="ew")
        self.create_quality_details_widgets(self.quality_details_frame)

        # --- Effects ---
        effects_frame = ttk.LabelFrame(parent_frame, text="Effects", padding=(10, 5))
        effects_frame.grid(row=current_row, column=0, columnspan=2, padx=5, pady=5, sticky="ew"); current_row += 1
        effects_frame.columnconfigure(0, weight=1)

        # Effect List
        effects_list_frame = ttk.Frame(effects_frame)
        effects_list_frame.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 5))
        effects_list_frame.columnconfigure(0, weight=1)
        effect_scrollbar = ttk.Scrollbar(effects_list_frame, orient=tk.VERTICAL)
        self.effects_list = tk.Listbox(effects_list_frame, height=3, yscrollcommand=effect_scrollbar.set, exportselection=False)
        effect_scrollbar.config(command=self.effects_list.yview)
        effect_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.effects_list.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        self.effects_list.bind('<<ListboxSelect>>', self.load_selected_effect)

        # Effect Buttons
        effect_button_frame = ttk.Frame(effects_frame)
        effect_button_frame.grid(row=1, column=0, columnspan=2, sticky="ew", pady=2)
        add_effect_button = ttk.Button(effect_button_frame, text="Add Effect", command=self.add_effect)
        add_effect_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(0, 2))
        remove_effect_button = ttk.Button(effect_button_frame, text="Remove Effect", command=self.remove_effect)
        remove_effect_button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(2, 0))

        # Effect Details
        self.effect_details_frame = ttk.LabelFrame(effects_frame, text="Effect Details", padding=(10, 5))
        self.effect_details_frame.grid(row=2, column=0, columnspan=2, padx=5, pady=5, sticky="ew")
        self.create_effect_details_widgets(self.effect_details_frame)

        # Initially disable drug details until a drug is selected
        self.set_widget_state(self.drug_details_frame, 'disabled')


    def create_quality_details_widgets(self, parent_frame):
        """Creates widgets for editing quality details (Schema Aligned)."""
        parent_frame.columnconfigure(1, weight=1) # Make entries expand
        current_row = 0

        ttk.Label(parent_frame, text="Type (str):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.quality_type_entry = ttk.Entry(parent_frame)
        self.quality_type_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Dollar Mult (float):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.quality_dollar_mult_entry = ttk.Entry(parent_frame)
        self.quality_dollar_mult_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Rep Mult (float):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.quality_rep_mult_entry = ttk.Entry(parent_frame)
        self.quality_rep_mult_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Unlock Rep (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.quality_unlock_rep_entry = ttk.Entry(parent_frame)
        self.quality_unlock_rep_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        # Initially disable quality details
        self.set_widget_state(self.quality_details_frame, 'disabled')


    def create_effect_details_widgets(self, parent_frame):
        """Creates widgets for editing effect details (Schema Aligned)."""
        parent_frame.columnconfigure(1, weight=1) # Make entries expand
        current_row = 0

        ttk.Label(parent_frame, text="Type (str):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.effect_type_entry = ttk.Entry(parent_frame)
        self.effect_type_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Unlock Rep (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.effect_unlock_rep_entry = ttk.Entry(parent_frame)
        self.effect_unlock_rep_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Probability (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.effect_probability_entry = ttk.Entry(parent_frame)
        self.effect_probability_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Dollar Mult (float):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.effect_dollar_mult_entry = ttk.Entry(parent_frame)
        self.effect_dollar_mult_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Rep Mult (float):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.effect_rep_mult_entry = ttk.Entry(parent_frame)
        self.effect_rep_mult_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1


        # Initially disable effect details
        self.set_widget_state(self.effect_details_frame, 'disabled')


    def create_shipping_details_widgets(self, parent_frame):
        """Creates widgets for editing shipping details (Schema Aligned)."""
        parent_frame.columnconfigure(1, weight=1) # Make entries expand
        current_row = 0

        ttk.Label(parent_frame, text="Name (str):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.shipping_name_entry = ttk.Entry(parent_frame) # Renamed from type
        self.shipping_name_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Cost (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.shipping_cost_entry = ttk.Entry(parent_frame)
        self.shipping_cost_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Unlock Rep (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.shipping_unlock_rep_entry = ttk.Entry(parent_frame)
        self.shipping_unlock_rep_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Min Amount (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.shipping_min_amount_entry = ttk.Entry(parent_frame) # Added
        self.shipping_min_amount_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Step Amount (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.shipping_step_amount_entry = ttk.Entry(parent_frame) # Added
        self.shipping_step_amount_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Max Amount (int):").grid(row=current_row, column=0, padx=5, pady=2, sticky="w")
        self.shipping_max_amount_entry = ttk.Entry(parent_frame) # Added
        self.shipping_max_amount_entry.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1


        # Initially disable shipping details
        self.set_widget_state(self.shipping_details_frame, 'disabled')


    def create_dialogue_details_widgets(self, parent_frame):
        """Creates widgets for editing dialogue details (Schema Aligned - using Text widgets)."""
        parent_frame.columnconfigure(1, weight=1) # Make text widgets expand
        parent_frame.columnconfigure(3, weight=1) # Make text widgets expand
        current_row = 0
        text_height = 4 # Height for Text widgets

        # --- Column 0 & 1 ---
        ttk.Label(parent_frame, text="Intro:").grid(row=current_row, column=0, padx=5, pady=2, sticky="nw")
        self.dialogue_intro_text = tk.Text(parent_frame, height=text_height, width=25)
        self.dialogue_intro_text.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Deal Start:").grid(row=current_row, column=0, padx=5, pady=2, sticky="nw")
        self.dialogue_dealStart_text = tk.Text(parent_frame, height=text_height, width=25)
        self.dialogue_dealStart_text.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Accept:").grid(row=current_row, column=0, padx=5, pady=2, sticky="nw")
        self.dialogue_accept_text = tk.Text(parent_frame, height=text_height, width=25)
        self.dialogue_accept_text.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Incomplete:").grid(row=current_row, column=0, padx=5, pady=2, sticky="nw")
        self.dialogue_incomplete_text = tk.Text(parent_frame, height=text_height, width=25)
        self.dialogue_incomplete_text.grid(row=current_row, column=1, padx=5, pady=2, sticky="ew"); current_row += 1

        # --- Column 2 & 3 ---
        current_row = 0 # Reset row for the second column
        ttk.Label(parent_frame, text="Expire:").grid(row=current_row, column=2, padx=5, pady=2, sticky="nw")
        self.dialogue_expire_text = tk.Text(parent_frame, height=text_height, width=25)
        self.dialogue_expire_text.grid(row=current_row, column=3, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Fail:").grid(row=current_row, column=2, padx=5, pady=2, sticky="nw")
        self.dialogue_fail_text = tk.Text(parent_frame, height=text_height, width=25)
        self.dialogue_fail_text.grid(row=current_row, column=3, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Success:").grid(row=current_row, column=2, padx=5, pady=2, sticky="nw")
        self.dialogue_success_text = tk.Text(parent_frame, height=text_height, width=25)
        self.dialogue_success_text.grid(row=current_row, column=3, padx=5, pady=2, sticky="ew"); current_row += 1

        ttk.Label(parent_frame, text="Reward:").grid(row=current_row, column=2, padx=5, pady=2, sticky="nw")
        self.dialogue_reward_text = tk.Text(parent_frame, height=text_height, width=25)
        self.dialogue_reward_text.grid(row=current_row, column=3, padx=5, pady=2, sticky="ew"); current_row += 1

    # --- Utility Methods ---

    def set_widget_state(self, parent_widget, state):
        """Recursively sets the state ('normal' or 'disabled') for widgets."""
        if isinstance(parent_widget, (ttk.Frame, ttk.LabelFrame, tk.Frame, tk.Canvas)):
             # Don't disable container widgets themselves, only their children
             pass
        else:
            try:
                parent_widget.configure(state=state)
            except tk.TclError:
                pass # Widget doesn't have a state option (like Scrollbar)

        # Recursively apply to children
        for child in parent_widget.winfo_children():
            # Skip scrollbars as disabling them is usually not desired
            if not isinstance(child, (ttk.Scrollbar, tk.Scrollbar)):
                self.set_widget_state(child, state)


    def clear_entry(self, entry):
        """Clears a ttk.Entry widget."""
        if entry: entry.delete(0, tk.END)

    def clear_text(self, text_widget):
        """Clears a tk.Text widget."""
        if text_widget: text_widget.delete('1.0', tk.END)

    def clear_listbox(self, listbox):
        """Clears a tk.Listbox widget."""
        if listbox: listbox.delete(0, tk.END)

    def sanitize_filename(self, name):
        """Removes or replaces characters invalid for filenames."""
        if not name:
            return "unnamed_dealer"
        # Remove characters that are definitely invalid on most systems
        name = re.sub(r'[\\/*?:"<>|]', "", name)
        # Replace spaces with underscores
        name = name.replace(" ", "_")
        # Limit length (optional, but good practice)
        return name[:100]

    def safe_float(self, value_str, default=0.0):
        """Safely converts a string to float, returning default on error."""
        try:
            return float(value_str)
        except (ValueError, TypeError):
            return default

    def safe_int(self, value_str, default=0):
        """Safely converts a string to int, returning default on error."""
        try:
            # Handle potential float strings before converting to int
            return int(float(value_str))
        except (ValueError, TypeError):
            return default

    def list_from_string(self, value_str, item_type=str, delimiter=','):
        """Converts a delimited string to a list of specified type."""
        if not value_str:
            return []
        items = [item.strip() for item in value_str.split(delimiter) if item.strip()]
        try:
            converted_items = []
            for item in items:
                if item_type == int:
                    converted_items.append(self.safe_int(item))
                elif item_type == float:
                    converted_items.append(self.safe_float(item))
                else:
                    converted_items.append(str(item)) # Ensure string type
            return converted_items
        except ValueError:
            messagebox.showerror("Conversion Error", f"Could not convert all items in '{value_str}' to {item_type.__name__} using delimiter '{delimiter}'.")
            return [] # Return empty list on conversion error

    def string_from_list(self, value_list, delimiter=', '):
        """Converts a list to a delimited string."""
        if not isinstance(value_list, list):
             return "" # Handle cases where data might be missing or wrong type
        return delimiter.join(map(str, value_list))

    def list_from_text(self, text_widget):
        """Reads lines from a Text widget into a list of strings."""
        content = text_widget.get("1.0", tk.END).strip()
        if not content:
            return []
        return [line.strip() for line in content.split('\n') if line.strip()]

    def text_from_list(self, text_widget, value_list):
        """Writes a list of strings into a Text widget, one per line."""
        self.clear_text(text_widget)
        if isinstance(value_list, list):
            text_widget.insert("1.0", "\n".join(map(str, value_list)))


    # --- Data Loading and Saving ---

    def update_dealer_listbox(self):
        """Updates the dealer listbox from the self.data."""
        # Store current selection
        current_selection = self.dealer_list.curselection()

        self.clear_listbox(self.dealer_list)
        for i, dealer in enumerate(self.data.get("dealers", [])):
            self.dealer_list.insert(tk.END, f"{i}: {dealer.get('name', 'Unnamed Dealer')}")

        # Restore selection if possible
        if current_selection:
            index = current_selection[0]
            if 0 <= index < self.dealer_list.size():
                self.dealer_list.selection_set(index)
                self.dealer_list.activate(index)
                self.dealer_list.see(index)
            else:
                 # Selection is now invalid, clear details
                 self.clear_dealer_details()


    def new_file(self):
        """Clears all data to start a new file."""
        # Optional: Check for unsaved changes before clearing
        # if self.has_unsaved_changes(): # (Need to implement change tracking)
        #     if not messagebox.askyesno("Unsaved Changes", "Discard unsaved changes?"):
        #         return

        if messagebox.askyesno("Confirm New File", "Discard current data and start a new file?"):
            self.data = {"dealers": []}
            self.current_dealer_index = -1
            self.file_path = None
            self.update_dealer_listbox()
            self.clear_dealer_details()
            self.root.title("Dealer Editor - New File")

    def load_json(self):
        """Loads dealer data from a JSON file."""
        # Optional: Check for unsaved changes
        path = filedialog.askopenfilename(
            title="Open Empire JSON File",
            defaultextension=".json",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
        )
        if not path:
            return

        try:
            with open(path, 'r', encoding='utf-8') as f:
                loaded_data = json.load(f)
                # Basic validation: Ensure 'dealers' key exists and is a list
                if isinstance(loaded_data, dict) and isinstance(loaded_data.get("dealers"), list):
                    self.data = loaded_data
                    self.file_path = path
                    self.root.title(f"Dealer Editor - {os.path.basename(self.file_path)}")
                else:
                     messagebox.showerror("Load Error", "Invalid JSON format. Root object must contain a 'dealers' list.")
                     return # Don't overwrite existing data if load failed validation

        except json.JSONDecodeError:
            messagebox.showerror("Load Error", f"Could not decode JSON file: {path}")
            return # Don't overwrite
        except Exception as e:
            messagebox.showerror("Load Error", f"An error occurred: {e}")
            return # Don't overwrite

        self.current_dealer_index = -1
        self.update_dealer_listbox()
        self.clear_dealer_details()

    def save_json(self):
        """Saves the current dealer data to the existing file or asks for a new one."""
        if not self.file_path:
            self.save_json_as() # If no file path exists, use Save As
        else:
            self._save_to_path(self.file_path)

    def save_json_as(self):
        """Saves the current dealer data to a new JSON file."""
        path = filedialog.asksaveasfilename(
            title="Save Empire JSON As...",
            defaultextension=".json",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")],
            initialfile="empire.json"
        )
        if not path:
            return
        self._save_to_path(path)

    def _save_to_path(self, path):
        """Helper function to save data to a specific path."""
        # Ensure the latest changes from the UI are saved if a dealer is selected
        if self.current_dealer_index != -1:
             # Silently save the currently displayed dealer before saving the whole file
             # to ensure UI changes are captured even if "Save Dealer" wasn't clicked last.
             if not self._save_dealer_data(self.current_dealer_index, show_success=False):
                 messagebox.showerror("Save Error", "Could not save current dealer's changes. File not saved.")
                 return # Abort saving the file if current dealer save fails

        try:
            with open(path, 'w', encoding='utf-8') as f:
                json.dump(self.data, f, indent=4) # Use indent for readability
            self.file_path = path
            self.root.title(f"Dealer Editor - {os.path.basename(self.file_path)}")
            # Don't show success message if called internally (e.g., from split)
            # messagebox.showinfo("Save Success", f"Data saved successfully to {path}")
        except Exception as e:
            messagebox.showerror("Save Error", f"An error occurred while saving to {path}: {e}")
            raise # Re-raise for internal calls to know about the failure

    # --- Split, Combine, Merge ---

    def split_dealers(self):
        """Splits each dealer into a separate JSON file in a chosen directory."""
        if not self.data or not self.data.get("dealers"):
            messagebox.showwarning("Split Error", "No dealer data loaded to split.")
            return

        # Ensure current dealer is saved before splitting
        if self.current_dealer_index != -1:
            if not self._save_dealer_data(self.current_dealer_index, show_success=False):
                 messagebox.showerror("Split Error", "Could not save current dealer's changes. Splitting aborted.")
                 return

        output_dir = filedialog.askdirectory(title="Select Directory to Save Split Dealer Files")
        if not output_dir:
            return

        success_count = 0
        error_count = 0
        for i, dealer in enumerate(self.data["dealers"]):
            dealer_name = dealer.get('name', f'Unnamed_Dealer_{i}')
            filename = self.sanitize_filename(dealer_name) + ".json"
            filepath = os.path.join(output_dir, filename)
            try:
                with open(filepath, 'w', encoding='utf-8') as f:
                    # Save only the single dealer object, not the {"dealers": [...]} structure
                    json.dump(dealer, f, indent=4)
                success_count += 1
            except Exception as e:
                error_count += 1
                print(f"Error saving {filepath}: {e}") # Log error to console

        if error_count > 0:
             messagebox.showerror("Split Complete (with errors)", f"Finished splitting.\nSuccessfully saved: {success_count}\nFailed: {error_count}\nCheck console for details.")
        else:
             messagebox.showinfo("Split Complete", f"Successfully saved {success_count} dealer files to:\n{output_dir}")


    def combine_dealers(self):
        """Combines multiple single-dealer JSON files into one empire file."""
        files_to_combine = filedialog.askopenfilenames(
            title="Select Dealer JSON Files to Combine",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
        )
        if not files_to_combine:
            return

        combined_dealers = []
        loaded_names = set() # To detect duplicates within the combined files
        errors = []
        duplicates = []

        for filepath in files_to_combine:
            try:
                with open(filepath, 'r', encoding='utf-8') as f:
                    # Assume each file contains a single dealer object
                    dealer_data = json.load(f)
                    if isinstance(dealer_data, dict) and 'name' in dealer_data:
                        dealer_name = dealer_data['name']
                        if dealer_name in loaded_names:
                            duplicates.append(f"'{dealer_name}' from {os.path.basename(filepath)}")
                        else:
                            combined_dealers.append(dealer_data)
                            loaded_names.add(dealer_name)
                    else:
                         # Maybe it's an empire file? Try loading its dealers list
                         if isinstance(dealer_data, dict) and isinstance(dealer_data.get("dealers"), list):
                             print(f"Info: Loading dealers list from {os.path.basename(filepath)}")
                             for sub_dealer in dealer_data["dealers"]:
                                 if isinstance(sub_dealer, dict) and 'name' in sub_dealer:
                                     sub_dealer_name = sub_dealer['name']
                                     if sub_dealer_name in loaded_names:
                                         duplicates.append(f"'{sub_dealer_name}' from {os.path.basename(filepath)}")
                                     else:
                                         combined_dealers.append(sub_dealer)
                                         loaded_names.add(sub_dealer_name)
                                 else:
                                     errors.append(f"Invalid sub-dealer format in {os.path.basename(filepath)}")
                         else:
                             errors.append(f"Invalid format in {os.path.basename(filepath)} (expected single dealer object or empire format)")

            except json.JSONDecodeError:
                errors.append(f"Could not decode JSON: {os.path.basename(filepath)}")
            except Exception as e:
                errors.append(f"Error reading {os.path.basename(filepath)}: {e}")

        if not combined_dealers:
             messagebox.showerror("Combine Error", "No valid dealer data found in selected files.")
             return

        # Report errors and duplicates before asking to save
        message = f"Found {len(combined_dealers)} unique dealers.\n"
        if errors:
            message += f"\nErrors loading files:\n- " + "\n- ".join(errors)
        if duplicates:
             message += f"\nDuplicate dealer names found (only first occurrence included):\n- " + "\n- ".join(duplicates)

        if errors or duplicates:
            messagebox.showwarning("Combine Issues", message)
        else:
             messagebox.showinfo("Combine Ready", message + "\nReady to save combined file.")


        save_path = filedialog.asksaveasfilename(
            title="Save Combined Empire File As...",
            defaultextension=".json",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")],
            initialfile="combined_empire.json"
        )
        if not save_path:
            return

        combined_data = {"dealers": combined_dealers}
        try:
            with open(save_path, 'w', encoding='utf-8') as f:
                json.dump(combined_data, f, indent=4)

            if messagebox.askyesno("Combine Success", f"Combined data saved successfully to:\n{save_path}\n\nLoad this file into the editor now?"):
                 # Load the newly created file
                 self.data = combined_data
                 self.file_path = save_path
                 self.root.title(f"Dealer Editor - {os.path.basename(self.file_path)}")
                 self.current_dealer_index = -1
                 self.update_dealer_listbox()
                 self.clear_dealer_details()

        except Exception as e:
            messagebox.showerror("Save Error", f"An error occurred while saving combined file: {e}")


    def merge_dealers(self):
        """Merges dealers from another empire JSON file into the current data."""
        if not self.data or not self.data.get("dealers"):
             if not messagebox.askyesno("Merge Warning", "No data currently loaded. Load the selected file instead of merging?"):
                 return
             else:
                 self.load_json() # Just load the file normally
                 return

        # Ensure current dealer is saved before merging
        if self.current_dealer_index != -1:
            if not self._save_dealer_data(self.current_dealer_index, show_success=False):
                 messagebox.showerror("Merge Error", "Could not save current dealer's changes. Merging aborted.")
                 return

        path_to_merge = filedialog.askopenfilename(
            title="Select Empire JSON File to Merge",
            defaultextension=".json",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
        )
        if not path_to_merge:
            return

        try:
            with open(path_to_merge, 'r', encoding='utf-8') as f:
                new_data = json.load(f)
                if not isinstance(new_data, dict) or not isinstance(new_data.get("dealers"), list):
                    messagebox.showerror("Merge Error", "Invalid JSON format in merge file. Root object must contain a 'dealers' list.")
                    return
        except json.JSONDecodeError:
            messagebox.showerror("Merge Error", f"Could not decode JSON file: {path_to_merge}")
            return
        except Exception as e:
            messagebox.showerror("Merge Error", f"An error occurred reading merge file: {e}")
            return

        # --- Merge Logic ---
        existing_dealers_map = {d.get('name'): i for i, d in enumerate(self.data['dealers']) if d.get('name')}
        added_count = 0
        overwritten_count = 0
        kept_count = 0
        merge_cancelled = False

        for new_dealer in new_data.get("dealers", []):
            new_dealer_name = new_dealer.get('name')
            if not new_dealer_name:
                print("Warning: Skipping dealer with no name during merge.")
                continue

            if new_dealer_name in existing_dealers_map:
                # Conflict! Ask user
                dialog = MergeConflictDialog(self.root, "Merge Conflict", new_dealer_name)
                action = dialog.result

                if action == "overwrite":
                    existing_index = existing_dealers_map[new_dealer_name]
                    self.data['dealers'][existing_index] = new_dealer
                    overwritten_count += 1
                    # No need to update map here as we only iterate through new dealers once
                elif action == "keep":
                    kept_count += 1
                    # Do nothing, keep the existing one
                else: # Cancelled
                     messagebox.showinfo("Merge Cancelled", "Merge operation cancelled by user.")
                     merge_cancelled = True
                     break # Stop merging
            else:
                # New dealer, just add
                self.data['dealers'].append(new_dealer)
                added_count += 1
                # Add to map in case the merge file itself has duplicates (though combine should prevent this)
                existing_dealers_map[new_dealer_name] = len(self.data['dealers']) - 1

        if not merge_cancelled:
             messagebox.showinfo("Merge Complete", f"Merge finished.\nAdded: {added_count}\nOverwritten: {overwritten_count}\nKept Existing: {kept_count}")
             self.update_dealer_listbox()
             self.clear_dealer_details()
             # Mark changes as unsaved (if implementing change tracking)


    # --- Dealer Management ---

    def add_dealer(self):
        """Adds a new dealer with default values (Schema Aligned)."""
        new_dealer = {
            "name": "New Dealer",
            "image": "",
            "dealTimes": [],        # int array
            "dealTimesMult": [],    # float array
            "penalties": [],        # int array
            "unlockRequirements": [], # string array
            "drugs": [],
            "shipping": [],
            "dialogue": {           # object with string arrays
                "intro": [],
                "dealStart": [],
                "accept": [],
                "incomplete": [],
                "expire": [],
                "fail": [],
                "success": [],
                "reward": []
            }
        }
        self.data["dealers"].append(new_dealer)
        new_index = len(self.data["dealers"]) - 1
        self.update_dealer_listbox()
        self.dealer_list.selection_clear(0, tk.END)
        self.dealer_list.selection_set(new_index)
        self.dealer_list.activate(new_index)
        self.dealer_list.see(new_index) # Ensure the new item is visible
        self.load_selected_dealer(None) # Load the newly added dealer

    def remove_dealer(self):
        """Removes the currently selected dealer."""
        selected_indices = self.dealer_list.curselection()
        if not selected_indices:
            messagebox.showwarning("Remove Error", "No dealer selected.")
            return

        index_to_remove = selected_indices[0]
        # Ensure index is valid before accessing data
        if 0 <= index_to_remove < len(self.data["dealers"]):
            dealer_name = self.data["dealers"][index_to_remove].get('name', 'Unnamed Dealer')

            if messagebox.askyesno("Confirm Removal", f"Are you sure you want to remove dealer '{dealer_name}'?"):
                del self.data["dealers"][index_to_remove]
                self.current_dealer_index = -1
                self.update_dealer_listbox()
                self.clear_dealer_details()
        else:
             messagebox.showerror("Remove Error", "Invalid dealer index selected.")
             # Optionally clear selection or listbox state here


    def load_selected_dealer(self, event):
        """Loads the details of the selected dealer into the UI fields (Schema Aligned)."""
        selected_indices = self.dealer_list.curselection()
        if not selected_indices:
            # If nothing is selected (e.g., after removal), clear details
            if self.current_dealer_index != -1: # Only clear if something *was* selected
                # Save previous dealer first? Only if changes were made.
                self.clear_dealer_details()
                self.current_dealer_index = -1
            return

        new_index = selected_indices[0]

        # Save previous dealer before loading next? (More robust check needed)
        if self.current_dealer_index != -1 and self.current_dealer_index != new_index:
            # Silently save previous if it was valid
            if 0 <= self.current_dealer_index < len(self.data['dealers']):
                self._save_dealer_data(self.current_dealer_index, show_success=False)

        self.current_dealer_index = new_index
        try:
            # Ensure index is still valid after potential saves/removals
            if not (0 <= self.current_dealer_index < len(self.data['dealers'])):
                 raise IndexError("Dealer index out of bounds after potential modification.")

            dealer = self.data["dealers"][self.current_dealer_index]

            # Enable details frame
            self.set_widget_state(self.details_scrollable_frame, 'normal')

            # --- Load Basic Info ---
            self.clear_entry(self.name_entry); self.name_entry.insert(0, dealer.get("name", ""))
            self.clear_entry(self.image_entry); self.image_entry.insert(0, dealer.get("image", ""))
            self.clear_entry(self.deal_times_entry); self.deal_times_entry.insert(0, self.string_from_list(dealer.get("dealTimes", []), delimiter=', ')) # int
            self.clear_entry(self.deal_times_mult_entry); self.deal_times_mult_entry.insert(0, self.string_from_list(dealer.get("dealTimesMult", []), delimiter=', ')) # float
            self.clear_entry(self.penalties_entry); self.penalties_entry.insert(0, self.string_from_list(dealer.get("penalties", []), delimiter=', ')) # int
            self.clear_entry(self.unlock_requirements_entry); self.unlock_requirements_entry.insert(0, self.string_from_list(dealer.get("unlockRequirements", []), delimiter=', ')) # string

            # --- Load Drugs ---
            self.update_drugs_listbox()
            self.clear_drug_details() # Clear details when loading new dealer

            # --- Load Shipping ---
            self.update_shipping_listbox()
            self.clear_shipping_details() # Clear details when loading new dealer

            # --- Load Dialogue ---
            dialogue = dealer.get("dialogue", {})
            self.text_from_list(self.dialogue_intro_text, dialogue.get("intro", []))
            self.text_from_list(self.dialogue_dealStart_text, dialogue.get("dealStart", []))
            self.text_from_list(self.dialogue_accept_text, dialogue.get("accept", []))
            self.text_from_list(self.dialogue_incomplete_text, dialogue.get("incomplete", []))
            self.text_from_list(self.dialogue_expire_text, dialogue.get("expire", []))
            self.text_from_list(self.dialogue_fail_text, dialogue.get("fail", []))
            self.text_from_list(self.dialogue_success_text, dialogue.get("success", []))
            self.text_from_list(self.dialogue_reward_text, dialogue.get("reward", []))

        except IndexError:
            messagebox.showerror("Error", "Selected dealer index is out of range.")
            self.clear_dealer_details()
            self.current_dealer_index = -1
        except Exception as e:
            messagebox.showerror("Load Error", f"Failed to load dealer details: {e}")
            # Optionally log the full traceback here
            print(f"Load Error Traceback: {e}")
            import traceback
            traceback.print_exc()
            self.clear_dealer_details()
            self.current_dealer_index = -1


    def save_current_dealer(self):
        """Saves the data from the UI fields back to the selected dealer in self.data."""
        if self.current_dealer_index == -1:
            messagebox.showwarning("Save Error", "No dealer selected to save.")
            return
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])):
             messagebox.showerror("Save Error", "Selected dealer index is invalid.")
             return

        if self._save_dealer_data(self.current_dealer_index, show_success=True):
             # Update the name in the listbox
             self.update_dealer_listbox()
             # Keep the selection (update_dealer_listbox handles this)


    def _save_dealer_data(self, index, show_success=True):
         """Internal helper to save data for a specific dealer index (Schema Aligned)."""
         try:
            # Double check index validity before proceeding
            if not (0 <= index < len(self.data['dealers'])):
                raise IndexError("Dealer index out of bounds during save.")

            dealer = self.data["dealers"][index]

            # --- Save Basic Info ---
            dealer["name"] = self.name_entry.get()
            dealer["image"] = self.image_entry.get()
            dealer["dealTimes"] = self.list_from_string(self.deal_times_entry.get(), int)
            dealer["dealTimesMult"] = self.list_from_string(self.deal_times_mult_entry.get(), float)
            dealer["penalties"] = self.list_from_string(self.penalties_entry.get(), int)
            dealer["unlockRequirements"] = self.list_from_string(self.unlock_requirements_entry.get(), str)

            # --- Save Drugs (including nested details) ---
            # Important: We need to save the currently edited drug *before* iterating or assuming list is up-to-date
            if self.current_drug_index != -1:
                 if not self._save_drug_data(self.current_drug_index, show_success=False):
                     # If saving the currently selected drug fails, abort saving the dealer
                     if show_success: # Only show error if this is the top-level save
                         messagebox.showerror("Save Error", "Failed to save current drug details. Dealer not saved.")
                     return False

            # The dealer['drugs'] list is modified directly by add/remove, and _save_drug_data updates the selected item.

            # --- Save Shipping (including nested details) ---
            if self.current_shipping_index != -1:
                 if not self._save_shipping_data(self.current_shipping_index, show_success=False):
                     if show_success:
                         messagebox.showerror("Save Error", "Failed to save current shipping details. Dealer not saved.")
                     return False

            # The dealer['shipping'] list is modified directly by add/remove.

            # --- Save Dialogue ---
            dialogue = dealer.get("dialogue", {}) # Get existing or create new dict
            dialogue["intro"] = self.list_from_text(self.dialogue_intro_text)
            dialogue["dealStart"] = self.list_from_text(self.dialogue_dealStart_text)
            dialogue["accept"] = self.list_from_text(self.dialogue_accept_text)
            dialogue["incomplete"] = self.list_from_text(self.dialogue_incomplete_text)
            dialogue["expire"] = self.list_from_text(self.dialogue_expire_text)
            dialogue["fail"] = self.list_from_text(self.dialogue_fail_text)
            dialogue["success"] = self.list_from_text(self.dialogue_success_text)
            dialogue["reward"] = self.list_from_text(self.dialogue_reward_text)
            dealer["dialogue"] = dialogue # Assign back to the dealer

            if show_success:
                messagebox.showinfo("Save Success", f"Dealer '{dealer.get('name', 'N/A')}' saved successfully.")
            return True # Indicate success

         except IndexError:
            if show_success:
                messagebox.showerror("Save Error", "Selected dealer index is out of range.")
            return False
         except Exception as e:
            if show_success:
                messagebox.showerror("Save Error", f"An error occurred while saving dealer: {e}")
                print(f"Save Error Traceback: {e}")
                import traceback
                traceback.print_exc()
            return False


    def clear_dealer_details(self):
        """Clears all dealer detail fields and disables them."""
        # Clear basic info
        self.clear_entry(self.name_entry)
        self.clear_entry(self.image_entry)
        self.clear_entry(self.deal_times_entry)
        self.clear_entry(self.deal_times_mult_entry)
        self.clear_entry(self.penalties_entry)
        self.clear_entry(self.unlock_requirements_entry)

        # Clear drugs section
        self.clear_listbox(self.drugs_list)
        self.clear_drug_details() # This handles nested clears and disabling

        # Clear shipping section
        self.clear_listbox(self.shipping_list)
        self.clear_shipping_details() # This handles disabling

        # Clear dialogue section
        self.clear_text(self.dialogue_intro_text)
        self.clear_text(self.dialogue_dealStart_text)
        self.clear_text(self.dialogue_accept_text)
        self.clear_text(self.dialogue_incomplete_text)
        self.clear_text(self.dialogue_expire_text)
        self.clear_text(self.dialogue_fail_text)
        self.clear_text(self.dialogue_success_text)
        self.clear_text(self.dialogue_reward_text)

        # Disable the entire details frame initially
        self.set_widget_state(self.details_scrollable_frame, 'disabled')
        self.current_dealer_index = -1 # Ensure index is reset
        self.current_drug_index = -1
        self.current_quality_index = -1
        self.current_effect_index = -1
        self.current_shipping_index = -1


    # --- Drug Section Management ---

    def update_drugs_listbox(self):
        """Updates the drugs listbox for the current dealer."""
        # Store current selection
        current_selection = self.drugs_list.curselection()

        self.clear_listbox(self.drugs_list)
        self.current_drug_index = -1 # Reset drug index
        if self.current_dealer_index != -1 and 0 <= self.current_dealer_index < len(self.data['dealers']):
            dealer = self.data["dealers"][self.current_dealer_index]
            for i, drug in enumerate(dealer.get("drugs", [])):
                self.drugs_list.insert(tk.END, f"{i}: {drug.get('type', 'Unnamed Drug')}")

            # Restore selection if possible
            if current_selection:
                index = current_selection[0]
                if 0 <= index < self.drugs_list.size():
                    self.drugs_list.selection_set(index)
                    self.drugs_list.activate(index)
                    self.drugs_list.see(index)
                    # Don't automatically clear details here, let load_selected_drug handle it
                # else: self.clear_drug_details() # Clear if selection invalid
        # else:
        #      self.clear_drug_details() # Clear if dealer index invalid


    def add_drug(self):
        """Adds a new drug to the current dealer (Schema Aligned)."""
        if self.current_dealer_index == -1:
            messagebox.showwarning("Add Error", "Select a dealer first.")
            return
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])):
             messagebox.showerror("Add Error", "Current dealer index is invalid.")
             return

        new_drug = {
            "type": "New Drug",         # string
            "bonus_dollar": 0,          # int
            "bonus_rep": 0,             # int
            "base_dollar_mult": 1,      # int (Schema says int, adjust if needed)
            "base_rep_mult": 1,         # int (Schema says int, adjust if needed)
            "unlockRep": 0,             # int
            "qualities": [],
            "effects": []
        }
        dealer = self.data["dealers"][self.current_dealer_index]
        if "drugs" not in dealer or not isinstance(dealer["drugs"], list):
            dealer["drugs"] = [] # Ensure it exists and is a list
        dealer["drugs"].append(new_drug)

        new_index = len(dealer["drugs"]) - 1
        self.update_drugs_listbox()
        self.drugs_list.selection_clear(0, tk.END)
        self.drugs_list.selection_set(new_index)
        self.drugs_list.activate(new_index)
        self.drugs_list.see(new_index)
        self.load_selected_drug(None) # Load the new drug

    def remove_drug(self):
        """Removes the selected drug from the current dealer."""
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            messagebox.showwarning("Remove Error", "Select a valid dealer first.")
            return

        selected_indices = self.drugs_list.curselection()
        if not selected_indices:
            messagebox.showwarning("Remove Error", "No drug selected.")
            return

        index_to_remove = selected_indices[0]
        dealer = self.data["dealers"][self.current_dealer_index]

        # Ensure index and drugs list are valid
        if isinstance(dealer.get("drugs"), list) and 0 <= index_to_remove < len(dealer["drugs"]):
            drug_name = dealer["drugs"][index_to_remove].get('type', 'Unnamed Drug')

            if messagebox.askyesno("Confirm Removal", f"Are you sure you want to remove drug '{drug_name}'?"):
                del dealer["drugs"][index_to_remove]
                self.current_drug_index = -1
                self.update_drugs_listbox()
                self.clear_drug_details()
        else:
             messagebox.showerror("Remove Error", "Invalid drug index or data structure.")


    def load_selected_drug(self, event):
        """Loads the details of the selected drug (Schema Aligned)."""
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            return # No valid dealer selected

        selected_indices = self.drugs_list.curselection()
        if not selected_indices:
            if self.current_drug_index != -1: # Only clear if something *was* selected
                self.clear_drug_details()
                self.current_drug_index = -1
            return

        new_index = selected_indices[0]

        # Save previous drug before loading next?
        if self.current_drug_index != -1 and self.current_drug_index != new_index:
             # Check validity before saving
             if 0 <= self.current_drug_index < len(self.data['dealers'][self.current_dealer_index].get('drugs',[])):
                 self._save_drug_data(self.current_drug_index, show_success=False) # Silently save previous

        self.current_drug_index = new_index
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            # Ensure drug index is valid for the current dealer's drug list
            if not isinstance(dealer.get('drugs'), list) or not (0 <= self.current_drug_index < len(dealer['drugs'])):
                 raise IndexError("Drug index out of bounds for current dealer.")

            drug = dealer["drugs"][self.current_drug_index]

            # Enable drug details frame
            self.set_widget_state(self.drug_details_frame, 'normal')

            # Load basic drug info (using schema names and types)
            self.clear_entry(self.drug_type_entry); self.drug_type_entry.insert(0, drug.get("type", "")) # str
            self.clear_entry(self.drug_bonus_dollar_entry); self.drug_bonus_dollar_entry.insert(0, str(drug.get("bonus_dollar", 0))) # int
            self.clear_entry(self.drug_bonus_rep_entry); self.drug_bonus_rep_entry.insert(0, str(drug.get("bonus_rep", 0))) # int
            self.clear_entry(self.drug_base_dollar_mult_entry); self.drug_base_dollar_mult_entry.insert(0, str(drug.get("base_dollar_mult", 1))) # int
            self.clear_entry(self.drug_base_rep_mult_entry); self.drug_base_rep_mult_entry.insert(0, str(drug.get("base_rep_mult", 1))) # int
            self.clear_entry(self.drug_unlock_rep_entry); self.drug_unlock_rep_entry.insert(0, str(drug.get("unlockRep", 0))) # int

            # Load qualities and effects
            self.update_qualities_listbox()
            self.clear_quality_details() # Handles disabling
            self.update_effects_listbox()
            self.clear_effect_details() # Handles disabling

        except (IndexError, KeyError) as e:
            messagebox.showerror("Load Error", f"Failed to load drug details: {e}")
            self.clear_drug_details()
            self.current_drug_index = -1
        except Exception as e:
             messagebox.showerror("Load Error", f"An unexpected error occurred loading drug: {e}")
             print(f"Load Drug Error Traceback: {e}")
             import traceback
             traceback.print_exc()
             self.clear_drug_details()
             self.current_drug_index = -1


    def _save_drug_data(self, index, show_success=True):
        """Internal helper to save data for a specific drug index (Schema Aligned)."""
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])):
            return False # No valid dealer selected

        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            # Ensure drug list and index are valid
            if not isinstance(dealer.get('drugs'), list) or not (0 <= index < len(dealer['drugs'])):
                 raise IndexError("Drug index out of bounds during save.")

            drug = dealer["drugs"][index]

            # Save basic drug info (using schema names and types)
            drug["type"] = self.drug_type_entry.get() # str
            drug["bonus_dollar"] = self.safe_int(self.drug_bonus_dollar_entry.get()) # int
            drug["bonus_rep"] = self.safe_int(self.drug_bonus_rep_entry.get()) # int
            drug["base_dollar_mult"] = self.safe_int(self.drug_base_dollar_mult_entry.get(), 1) # int
            drug["base_rep_mult"] = self.safe_int(self.drug_base_rep_mult_entry.get(), 1) # int
            drug["unlockRep"] = self.safe_int(self.drug_unlock_rep_entry.get()) # int

            # Save nested qualities and effects
            if self.current_quality_index != -1:
                if not self._save_quality_data(self.current_quality_index, show_success=False):
                     # Abort drug save if quality save fails
                     if show_success: messagebox.showerror("Save Error", "Failed to save current quality details. Drug not saved.")
                     return False
            if self.current_effect_index != -1:
                if not self._save_effect_data(self.current_effect_index, show_success=False):
                     # Abort drug save if effect save fails
                     if show_success: messagebox.showerror("Save Error", "Failed to save current effect details. Drug not saved.")
                     return False

            # Update listbox entry requires access to the listbox itself, better done in the calling function (save_current_dealer) via update_drugs_listbox
            # self.drugs_list.delete(index)
            # self.drugs_list.insert(index, f"{index}: {drug.get('type', 'Unnamed Drug')}")
            # self.drugs_list.selection_set(index)

            return True # Success

        except (IndexError, KeyError) as e:
            if show_success:
                 messagebox.showerror("Save Error", f"Failed to save drug details (Index/Key Error): {e}")
            return False
        except Exception as e:
            if show_success:
                 messagebox.showerror("Save Error", f"An error occurred saving drug: {e}")
                 print(f"Save Drug Error Traceback: {e}")
                 import traceback
                 traceback.print_exc()
            return False


    def clear_drug_details(self):
        """Clears drug detail fields and disables the frame."""
        self.clear_entry(self.drug_type_entry)
        self.clear_entry(self.drug_bonus_dollar_entry)
        self.clear_entry(self.drug_bonus_rep_entry)
        self.clear_entry(self.drug_base_dollar_mult_entry)
        self.clear_entry(self.drug_base_rep_mult_entry)
        self.clear_entry(self.drug_unlock_rep_entry)

        self.clear_listbox(self.qualities_list)
        self.clear_quality_details() # Handles disabling quality frame
        self.clear_listbox(self.effects_list)
        self.clear_effect_details() # Handles disabling effect frame

        self.set_widget_state(self.drug_details_frame, 'disabled')
        self.current_drug_index = -1
        self.current_quality_index = -1 # Also reset nested indices
        self.current_effect_index = -1


    # --- Quality Section Management (within Drug) ---

    def update_qualities_listbox(self):
        """Updates the qualities listbox for the current drug."""
        current_selection = self.qualities_list.curselection()
        self.clear_listbox(self.qualities_list)
        self.current_quality_index = -1

        if self.current_dealer_index != -1 and self.current_drug_index != -1 and \
           0 <= self.current_dealer_index < len(self.data['dealers']):
            try:
                dealer = self.data["dealers"][self.current_dealer_index]
                if isinstance(dealer.get('drugs'), list) and 0 <= self.current_drug_index < len(dealer['drugs']):
                    drug = dealer["drugs"][self.current_drug_index]
                    for i, quality in enumerate(drug.get("qualities", [])):
                        self.qualities_list.insert(tk.END, f"{i}: {quality.get('type', 'Unnamed Quality')}")

                    # Restore selection
                    if current_selection:
                        index = current_selection[0]
                        if 0 <= index < self.qualities_list.size():
                            self.qualities_list.selection_set(index)
                            self.qualities_list.activate(index)
                            self.qualities_list.see(index)
                        # else: self.clear_quality_details() # Clear if invalid
                # else: self.clear_quality_details() # Clear if drug index invalid
            except (IndexError, KeyError):
                # self.clear_quality_details() # Clear on error
                pass # Ignore if indices are invalid during update
        # else:
        #      self.clear_quality_details() # Clear if dealer/drug index invalid


    def add_quality(self):
        """Adds a new quality to the current drug (Schema Aligned)."""
        if self.current_dealer_index == -1 or self.current_drug_index == -1:
            messagebox.showwarning("Add Error", "Select a dealer and a drug first.")
            return
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])): return # Invalid dealer index

        new_quality = {
            "type": "New Quality",      # string
            "dollar_mult": 1.0,         # float/number
            "rep_mult": 1.0,            # float/number
            "unlockRep": 0              # int
        }
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('drugs'), list) or not (0 <= self.current_drug_index < len(dealer['drugs'])):
                 messagebox.showerror("Add Error", "Current drug selection is invalid.")
                 return

            drug = dealer["drugs"][self.current_drug_index]
            if "qualities" not in drug or not isinstance(drug["qualities"], list):
                drug["qualities"] = [] # Ensure list exists
            drug["qualities"].append(new_quality)

            new_index = len(drug["qualities"]) - 1
            self.update_qualities_listbox()
            self.qualities_list.selection_clear(0, tk.END)
            self.qualities_list.selection_set(new_index)
            self.qualities_list.activate(new_index)
            self.qualities_list.see(new_index)
            self.load_selected_quality(None)
        except (IndexError, KeyError) as e:
             messagebox.showerror("Add Error", f"Could not add quality: {e}")

    def remove_quality(self):
        """Removes the selected quality."""
        if self.current_dealer_index == -1 or self.current_drug_index == -1: return
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])): return

        selected_indices = self.qualities_list.curselection()
        if not selected_indices:
            messagebox.showwarning("Remove Error", "No quality selected.")
            return

        index_to_remove = selected_indices[0]
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('drugs'), list) or not (0 <= self.current_drug_index < len(dealer['drugs'])): return

            drug = dealer["drugs"][self.current_drug_index]
            if not isinstance(drug.get('qualities'), list) or not (0 <= index_to_remove < len(drug['qualities'])): return

            quality_name = drug["qualities"][index_to_remove].get('type', 'Unnamed Quality')
            if messagebox.askyesno("Confirm Removal", f"Remove quality '{quality_name}'?"):
                del drug["qualities"][index_to_remove]
                self.current_quality_index = -1
                self.update_qualities_listbox()
                self.clear_quality_details() # Disables frame
        except (IndexError, KeyError) as e:
             messagebox.showerror("Remove Error", f"Could not remove quality: {e}")

    def load_selected_quality(self, event):
        """Loads the details of the selected quality (Schema Aligned)."""
        if self.current_dealer_index == -1 or self.current_drug_index == -1: return
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])): return

        selected_indices = self.qualities_list.curselection()
        if not selected_indices:
             if self.current_quality_index != -1:
                 self.clear_quality_details()
                 self.current_quality_index = -1
             return

        new_index = selected_indices[0]

        if self.current_quality_index != -1 and self.current_quality_index != new_index:
             # Check validity before saving
             try:
                 if 0 <= self.current_quality_index < len(self.data['dealers'][self.current_dealer_index]['drugs'][self.current_drug_index].get('qualities',[])):
                     self._save_quality_data(self.current_quality_index, show_success=False) # Silently save previous
             except (IndexError, KeyError): pass # Ignore if indices became invalid

        self.current_quality_index = new_index
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('drugs'), list) or not (0 <= self.current_drug_index < len(dealer['drugs'])): raise IndexError("Invalid drug index")
            drug = dealer["drugs"][self.current_drug_index]
            if not isinstance(drug.get('qualities'), list) or not (0 <= self.current_quality_index < len(drug['qualities'])): raise IndexError("Invalid quality index")

            quality = drug["qualities"][self.current_quality_index]

            self.set_widget_state(self.quality_details_frame, 'normal')

            self.clear_entry(self.quality_type_entry); self.quality_type_entry.insert(0, quality.get("type", "")) # str
            self.clear_entry(self.quality_dollar_mult_entry); self.quality_dollar_mult_entry.insert(0, str(quality.get("dollar_mult", 1.0))) # float
            self.clear_entry(self.quality_rep_mult_entry); self.quality_rep_mult_entry.insert(0, str(quality.get("rep_mult", 1.0))) # float
            self.clear_entry(self.quality_unlock_rep_entry); self.quality_unlock_rep_entry.insert(0, str(quality.get("unlockRep", 0))) # int

        except (IndexError, KeyError) as e:
            messagebox.showerror("Load Error", f"Failed to load quality details: {e}")
            self.clear_quality_details()
            self.current_quality_index = -1
        except Exception as e:
             messagebox.showerror("Load Error", f"An unexpected error occurred loading quality: {e}")
             print(f"Load Quality Error Traceback: {e}")
             import traceback
             traceback.print_exc()
             self.clear_quality_details()
             self.current_quality_index = -1

    def _save_quality_data(self, index, show_success=True):
        """Internal helper to save data for a specific quality index (Schema Aligned)."""
        if self.current_dealer_index == -1 or self.current_drug_index == -1: return False
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])): return False

        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('drugs'), list) or not (0 <= self.current_drug_index < len(dealer['drugs'])): raise IndexError("Invalid drug index")
            drug = dealer["drugs"][self.current_drug_index]
            if not isinstance(drug.get('qualities'), list) or not (0 <= index < len(drug['qualities'])): raise IndexError("Invalid quality index")

            quality = drug["qualities"][index]

            quality["type"] = self.quality_type_entry.get() # str
            quality["dollar_mult"] = self.safe_float(self.quality_dollar_mult_entry.get(), 1.0) # float
            quality["rep_mult"] = self.safe_float(self.quality_rep_mult_entry.get(), 1.0) # float
            quality["unlockRep"] = self.safe_int(self.quality_unlock_rep_entry.get()) # int

            return True # Success

        except (IndexError, KeyError) as e:
            if show_success: messagebox.showerror("Save Error", f"Failed to save quality details (Index/Key Error): {e}")
            return False
        except Exception as e:
            if show_success:
                 messagebox.showerror("Save Error", f"An error occurred saving quality: {e}")
                 print(f"Save Quality Error Traceback: {e}")
                 import traceback
                 traceback.print_exc()
            return False


    def clear_quality_details(self):
        """Clears quality detail fields and disables the frame."""
        self.clear_entry(self.quality_type_entry)
        self.clear_entry(self.quality_dollar_mult_entry)
        self.clear_entry(self.quality_rep_mult_entry)
        self.clear_entry(self.quality_unlock_rep_entry)
        self.set_widget_state(self.quality_details_frame, 'disabled')
        self.current_quality_index = -1


    # --- Effect Section Management (within Drug) ---

    def update_effects_listbox(self):
        """Updates the effects listbox for the current drug."""
        current_selection = self.effects_list.curselection()
        self.clear_listbox(self.effects_list)
        self.current_effect_index = -1

        if self.current_dealer_index != -1 and self.current_drug_index != -1 and \
           0 <= self.current_dealer_index < len(self.data['dealers']):
            try:
                dealer = self.data["dealers"][self.current_dealer_index]
                if isinstance(dealer.get('drugs'), list) and 0 <= self.current_drug_index < len(dealer['drugs']):
                    drug = dealer["drugs"][self.current_drug_index]
                    for i, effect in enumerate(drug.get("effects", [])):
                        self.effects_list.insert(tk.END, f"{i}: {effect.get('type', 'Unnamed Effect')}")

                    # Restore selection
                    if current_selection:
                        index = current_selection[0]
                        if 0 <= index < self.effects_list.size():
                            self.effects_list.selection_set(index)
                            self.effects_list.activate(index)
                            self.effects_list.see(index)
                        # else: self.clear_effect_details()
                # else: self.clear_effect_details()
            except (IndexError, KeyError):
                # self.clear_effect_details()
                pass
        # else:
        #      self.clear_effect_details()


    def add_effect(self):
        """Adds a new effect to the current drug (Schema Aligned)."""
        if self.current_dealer_index == -1 or self.current_drug_index == -1:
            messagebox.showwarning("Add Error", "Select a dealer and a drug first.")
            return
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])): return

        new_effect = {
            "type": "New Effect",       # string
            "unlockRep": 0,             # int
            "probability": 0,           # int (Schema says int)
            "dollar_mult": 1.0,         # float/number
            "rep_mult": 1.0             # float/number
        }
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('drugs'), list) or not (0 <= self.current_drug_index < len(dealer['drugs'])): return

            drug = dealer["drugs"][self.current_drug_index]
            if "effects" not in drug or not isinstance(drug["effects"], list):
                drug["effects"] = [] # Ensure list exists
            drug["effects"].append(new_effect)

            new_index = len(drug["effects"]) - 1
            self.update_effects_listbox()
            self.effects_list.selection_clear(0, tk.END)
            self.effects_list.selection_set(new_index)
            self.effects_list.activate(new_index)
            self.effects_list.see(new_index)
            self.load_selected_effect(None)
        except (IndexError, KeyError) as e:
             messagebox.showerror("Add Error", f"Could not add effect: {e}")


    def remove_effect(self):
        """Removes the selected effect."""
        if self.current_dealer_index == -1 or self.current_drug_index == -1: return
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])): return

        selected_indices = self.effects_list.curselection()
        if not selected_indices:
            messagebox.showwarning("Remove Error", "No effect selected.")
            return

        index_to_remove = selected_indices[0]
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('drugs'), list) or not (0 <= self.current_drug_index < len(dealer['drugs'])): return
            drug = dealer["drugs"][self.current_drug_index]
            if not isinstance(drug.get('effects'), list) or not (0 <= index_to_remove < len(drug['effects'])): return

            effect_name = drug["effects"][index_to_remove].get('type', 'Unnamed Effect')
            if messagebox.askyesno("Confirm Removal", f"Remove effect '{effect_name}'?"):
                del drug["effects"][index_to_remove]
                self.current_effect_index = -1
                self.update_effects_listbox()
                self.clear_effect_details() # Disables frame
        except (IndexError, KeyError) as e:
             messagebox.showerror("Remove Error", f"Could not remove effect: {e}")


    def load_selected_effect(self, event):
        """Loads the details of the selected effect (Schema Aligned)."""
        if self.current_dealer_index == -1 or self.current_drug_index == -1: return
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])): return

        selected_indices = self.effects_list.curselection()
        if not selected_indices:
             if self.current_effect_index != -1:
                 self.clear_effect_details()
                 self.current_effect_index = -1
             return

        new_index = selected_indices[0]

        if self.current_effect_index != -1 and self.current_effect_index != new_index:
             # Check validity before saving
             try:
                  if 0 <= self.current_effect_index < len(self.data['dealers'][self.current_dealer_index]['drugs'][self.current_drug_index].get('effects',[])):
                      self._save_effect_data(self.current_effect_index, show_success=False) # Silently save previous
             except (IndexError, KeyError): pass # Ignore if indices became invalid

        self.current_effect_index = new_index
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('drugs'), list) or not (0 <= self.current_drug_index < len(dealer['drugs'])): raise IndexError("Invalid drug index")
            drug = dealer["drugs"][self.current_drug_index]
            if not isinstance(drug.get('effects'), list) or not (0 <= self.current_effect_index < len(drug['effects'])): raise IndexError("Invalid effect index")

            effect = drug["effects"][self.current_effect_index]

            self.set_widget_state(self.effect_details_frame, 'normal')

            self.clear_entry(self.effect_type_entry); self.effect_type_entry.insert(0, effect.get("type", "")) # str
            self.clear_entry(self.effect_unlock_rep_entry); self.effect_unlock_rep_entry.insert(0, str(effect.get("unlockRep", 0))) # int
            self.clear_entry(self.effect_probability_entry); self.effect_probability_entry.insert(0, str(effect.get("probability", 0))) # int
            self.clear_entry(self.effect_dollar_mult_entry); self.effect_dollar_mult_entry.insert(0, str(effect.get("dollar_mult", 1.0))) # float
            self.clear_entry(self.effect_rep_mult_entry); self.effect_rep_mult_entry.insert(0, str(effect.get("rep_mult", 1.0))) # float

        except (IndexError, KeyError) as e:
            messagebox.showerror("Load Error", f"Failed to load effect details: {e}")
            self.clear_effect_details()
            self.current_effect_index = -1
        except Exception as e:
             messagebox.showerror("Load Error", f"An unexpected error occurred loading effect: {e}")
             print(f"Load Effect Error Traceback: {e}")
             import traceback
             traceback.print_exc()
             self.clear_effect_details()
             self.current_effect_index = -1


    def _save_effect_data(self, index, show_success=True):
        """Internal helper to save data for a specific effect index (Schema Aligned)."""
        if self.current_dealer_index == -1 or self.current_drug_index == -1: return False
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])): return False

        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('drugs'), list) or not (0 <= self.current_drug_index < len(dealer['drugs'])): raise IndexError("Invalid drug index")
            drug = dealer["drugs"][self.current_drug_index]
            if not isinstance(drug.get('effects'), list) or not (0 <= index < len(drug['effects'])): raise IndexError("Invalid effect index")

            effect = drug["effects"][index]

            effect["type"] = self.effect_type_entry.get() # str
            effect["unlockRep"] = self.safe_int(self.effect_unlock_rep_entry.get()) # int
            effect["probability"] = self.safe_int(self.effect_probability_entry.get()) # int
            effect["dollar_mult"] = self.safe_float(self.effect_dollar_mult_entry.get(), 1.0) # float
            effect["rep_mult"] = self.safe_float(self.effect_rep_mult_entry.get(), 1.0) # float

            return True # Success

        except (IndexError, KeyError) as e:
            if show_success: messagebox.showerror("Save Error", f"Failed to save effect details (Index/Key Error): {e}")
            return False
        except Exception as e:
            if show_success:
                 messagebox.showerror("Save Error", f"An error occurred saving effect: {e}")
                 print(f"Save Effect Error Traceback: {e}")
                 import traceback
                 traceback.print_exc()
            return False


    def clear_effect_details(self):
        """Clears effect detail fields and disables the frame."""
        self.clear_entry(self.effect_type_entry)
        self.clear_entry(self.effect_unlock_rep_entry)
        self.clear_entry(self.effect_probability_entry)
        self.clear_entry(self.effect_dollar_mult_entry)
        self.clear_entry(self.effect_rep_mult_entry)
        self.set_widget_state(self.effect_details_frame, 'disabled')
        self.current_effect_index = -1


    # --- Shipping Section Management ---

    def update_shipping_listbox(self):
        """Updates the shipping listbox for the current dealer."""
        current_selection = self.shipping_list.curselection()
        self.clear_listbox(self.shipping_list)
        self.current_shipping_index = -1

        if self.current_dealer_index != -1 and 0 <= self.current_dealer_index < len(self.data['dealers']):
            dealer = self.data["dealers"][self.current_dealer_index]
            for i, shipping in enumerate(dealer.get("shipping", [])):
                # Use 'name' field according to schema
                self.shipping_list.insert(tk.END, f"{i}: {shipping.get('name', 'Unnamed Shipping')}")

            # Restore selection
            if current_selection:
                index = current_selection[0]
                if 0 <= index < self.shipping_list.size():
                    self.shipping_list.selection_set(index)
                    self.shipping_list.activate(index)
                    self.shipping_list.see(index)
                # else: self.clear_shipping_details()
        # else:
        #      self.clear_shipping_details()


    def add_shipping(self):
        """Adds a new shipping option to the current dealer (Schema Aligned)."""
        if self.current_dealer_index == -1:
            messagebox.showwarning("Add Error", "Select a dealer first.")
            return
        if not (0 <= self.current_dealer_index < len(self.data['dealers'])): return

        new_shipping = {
            "name": "New Shipping",     # string
            "cost": 0,                  # int
            "unlockRep": 0,             # int
            "minAmount": 0,             # int
            "stepAmount": 1,            # int (default to 1?)
            "maxAmount": 100            # int (default to 100?)
        }
        dealer = self.data["dealers"][self.current_dealer_index]
        if "shipping" not in dealer or not isinstance(dealer["shipping"], list):
            dealer["shipping"] = [] # Ensure list exists
        dealer["shipping"].append(new_shipping)

        new_index = len(dealer["shipping"]) - 1
        self.update_shipping_listbox()
        self.shipping_list.selection_clear(0, tk.END)
        self.shipping_list.selection_set(new_index)
        self.shipping_list.activate(new_index)
        self.shipping_list.see(new_index)
        self.load_selected_shipping(None)

    def remove_shipping(self):
        """Removes the selected shipping option."""
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])): return

        selected_indices = self.shipping_list.curselection()
        if not selected_indices:
            messagebox.showwarning("Remove Error", "No shipping option selected.")
            return

        index_to_remove = selected_indices[0]
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('shipping'), list) or not (0 <= index_to_remove < len(dealer['shipping'])): return

            # Use 'name' field according to schema
            shipping_name = dealer["shipping"][index_to_remove].get('name', 'Unnamed Shipping')

            if messagebox.askyesno("Confirm Removal", f"Remove shipping option '{shipping_name}'?"):
                del dealer["shipping"][index_to_remove]
                self.current_shipping_index = -1
                self.update_shipping_listbox()
                self.clear_shipping_details() # Disables frame
        except (IndexError, KeyError) as e:
             messagebox.showerror("Remove Error", f"Could not remove shipping option: {e}")


    def load_selected_shipping(self, event):
        """Loads the details of the selected shipping option (Schema Aligned)."""
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])): return

        selected_indices = self.shipping_list.curselection()
        if not selected_indices:
             if self.current_shipping_index != -1:
                 self.clear_shipping_details()
                 self.current_shipping_index = -1
             return

        new_index = selected_indices[0]

        if self.current_shipping_index != -1 and self.current_shipping_index != new_index:
             # Check validity before saving
             try:
                 if 0 <= self.current_shipping_index < len(self.data['dealers'][self.current_dealer_index].get('shipping',[])):
                     self._save_shipping_data(self.current_shipping_index, show_success=False) # Silently save previous
             except (IndexError, KeyError): pass

        self.current_shipping_index = new_index
        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('shipping'), list) or not (0 <= self.current_shipping_index < len(dealer['shipping'])): raise IndexError("Invalid shipping index")

            shipping = dealer["shipping"][self.current_shipping_index]

            self.set_widget_state(self.shipping_details_frame, 'normal')

            # Use schema fields
            self.clear_entry(self.shipping_name_entry); self.shipping_name_entry.insert(0, shipping.get("name", "")) # str
            self.clear_entry(self.shipping_cost_entry); self.shipping_cost_entry.insert(0, str(shipping.get("cost", 0))) # int
            self.clear_entry(self.shipping_unlock_rep_entry); self.shipping_unlock_rep_entry.insert(0, str(shipping.get("unlockRep", 0))) # int
            self.clear_entry(self.shipping_min_amount_entry); self.shipping_min_amount_entry.insert(0, str(shipping.get("minAmount", 0))) # int
            self.clear_entry(self.shipping_step_amount_entry); self.shipping_step_amount_entry.insert(0, str(shipping.get("stepAmount", 1))) # int
            self.clear_entry(self.shipping_max_amount_entry); self.shipping_max_amount_entry.insert(0, str(shipping.get("maxAmount", 100))) # int

        except (IndexError, KeyError) as e:
            messagebox.showerror("Load Error", f"Failed to load shipping details: {e}")
            self.clear_shipping_details()
            self.current_shipping_index = -1
        except Exception as e:
             messagebox.showerror("Load Error", f"An unexpected error occurred loading shipping: {e}")
             print(f"Load Shipping Error Traceback: {e}")
             import traceback
             traceback.print_exc()
             self.clear_shipping_details()
             self.current_shipping_index = -1


    def _save_shipping_data(self, index, show_success=True):
        """Internal helper to save data for a specific shipping index (Schema Aligned)."""
        if self.current_dealer_index == -1 or not (0 <= self.current_dealer_index < len(self.data['dealers'])): return False

        try:
            dealer = self.data["dealers"][self.current_dealer_index]
            if not isinstance(dealer.get('shipping'), list) or not (0 <= index < len(dealer['shipping'])): raise IndexError("Invalid shipping index")

            shipping = dealer["shipping"][index]

            # Use schema fields
            shipping["name"] = self.shipping_name_entry.get() # str
            shipping["cost"] = self.safe_int(self.shipping_cost_entry.get()) # int
            shipping["unlockRep"] = self.safe_int(self.shipping_unlock_rep_entry.get()) # int
            shipping["minAmount"] = self.safe_int(self.shipping_min_amount_entry.get()) # int
            shipping["stepAmount"] = self.safe_int(self.shipping_step_amount_entry.get(), 1) # int
            shipping["maxAmount"] = self.safe_int(self.shipping_max_amount_entry.get(), 100) # int

            return True # Success

        except (IndexError, KeyError) as e:
            if show_success: messagebox.showerror("Save Error", f"Failed to save shipping details (Index/Key Error): {e}")
            return False
        except Exception as e:
            if show_success:
                 messagebox.showerror("Save Error", f"An error occurred saving shipping: {e}")
                 print(f"Save Shipping Error Traceback: {e}")
                 import traceback
                 traceback.print_exc()
            return False


    def clear_shipping_details(self):
        """Clears shipping detail fields and disables the frame."""
        self.clear_entry(self.shipping_name_entry)
        self.clear_entry(self.shipping_cost_entry)
        self.clear_entry(self.shipping_unlock_rep_entry)
        self.clear_entry(self.shipping_min_amount_entry)
        self.clear_entry(self.shipping_step_amount_entry)
        self.clear_entry(self.shipping_max_amount_entry)
        self.set_widget_state(self.shipping_details_frame, 'disabled')
        self.current_shipping_index = -1


if __name__ == "__main__":
    root = tk.Tk()
    # Optional: Apply a theme for a more modern look
    style = ttk.Style(root)
    try:
        # Try common modern themes
        if os.name == 'nt': # Windows
            style.theme_use('vista')
        elif os.name == 'posix': # Linux/MacOS (clam is usually available)
             # Check available themes
             available_themes = style.theme_names()
             # print("Available themes:", available_themes) # For debugging
             if 'clam' in available_themes:
                 style.theme_use('clam')
             elif 'aqua' in available_themes: # MacOS specific
                 style.theme_use('aqua')
             # else default theme is used
    except tk.TclError:
        print("ttk theme not found, using default.")

    app = DealerEditorApp(root)
    root.mainloop()
