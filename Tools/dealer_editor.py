import tkinter as tk
from tkinter import ttk, filedialog, messagebox, simpledialog
import json
import os
import re # For sanitizing filenames
import copy # For creating default list items

# --- Constants ---
DEFAULT_NEW_STRING = "new_string"
DEFAULT_NEW_NUMBER = 0
DEFAULT_NEW_BOOL = False
DEFAULT_NEW_DEALER_NAME = "New Dealer"
DEFAULT_TEMPLATE_NAME_SUFFIX = " (Copy)"
# Option for selection dialogs
ADD_DEFAULT_OPTION = "[Add Default New Item]"

# --- Core Helper Functions ---

def sanitize_filename(name):
    """Removes characters that are problematic in filenames."""
    name = re.sub(r'[\\/*?:"<>|]', "", name)
    name = name.replace(" ", "_")
    if not name:
        name = "unnamed_dealer"
    return name

def load_json(filepath):
    """Loads JSON data from a file, showing errors."""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            return json.load(f)
    except FileNotFoundError:
        messagebox.showerror("Error", f"File not found: {filepath}")
    except json.JSONDecodeError as e:
        messagebox.showerror("Error", f"Invalid JSON file: {filepath}\n{e}")
    except Exception as e:
        messagebox.showerror("Error", f"An unexpected error occurred loading {filepath}:\n{e}")
    return None

def save_json(data, filepath):
    """Saves data to a JSON file with pretty printing, showing errors."""
    try:
        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        return True
    except Exception as e:
        messagebox.showerror("Error", f"Failed to save file: {filepath}\n{e}")
        return False

def create_new_item_from_template(template_item):
    """Creates a new item based on the type of the template item, resetting values."""
    if isinstance(template_item, dict):
        try:
            new_item = copy.deepcopy(template_item)
            # Reset values in the new copy recursively
            _reset_dict_values(new_item)
            # Add suffix to name/type if they exist
            if "name" in new_item:
                 original_name = str(template_item.get("name", "New Item"))
                 # Avoid adding suffix multiple times
                 if not original_name.endswith(DEFAULT_TEMPLATE_NAME_SUFFIX):
                     new_item["name"] = original_name + DEFAULT_TEMPLATE_NAME_SUFFIX
                 else:
                     new_item["name"] = original_name # Keep existing if already a copy
            if "type" in new_item:
                 original_type = str(template_item.get("type", "New Type"))
                 if not original_type.endswith(DEFAULT_TEMPLATE_NAME_SUFFIX):
                    new_item["type"] = original_type + DEFAULT_TEMPLATE_NAME_SUFFIX
                 else:
                    new_item["type"] = original_type
            # Reset common numeric fields
            for key in ["unlockRep", "bonus_dollar", "bonus_rep", "base_dollar_mult", "base_rep_mult", "dollar_mult", "rep_mult", "probability", "cost", "minAmount", "stepAmount", "maxAmount"]:
                 if key in new_item and isinstance(new_item[key], (int, float)):
                     new_item[key] = DEFAULT_NEW_NUMBER
            return new_item
        except Exception as e:
            print(f"Error deep copying/resetting dict template: {e}")
            return {} # Fallback to empty dict
    elif isinstance(template_item, str):
        return DEFAULT_NEW_STRING
    elif isinstance(template_item, (int, float)):
        return DEFAULT_NEW_NUMBER
    elif isinstance(template_item, bool):
        return DEFAULT_NEW_BOOL
    elif isinstance(template_item, list):
        return []
    else: # Fallback for None or other types
        return None

def _reset_dict_values(d):
    """Helper to recursively reset values in a copied dictionary."""
    for key, value in d.items():
        # Skip keys we often want to preserve or handle specially after copy
        if key in ["name", "type"]: continue

        if isinstance(value, list): d[key] = []
        elif isinstance(value, dict): _reset_dict_values(value) # Recurse
        elif isinstance(value, str): d[key] = "new_value"
        elif isinstance(value, (int, float)): d[key] = DEFAULT_NEW_NUMBER
        elif isinstance(value, bool): d[key] = DEFAULT_NEW_BOOL
        else: d[key] = None


# --- File Operation Functions ---

def split_empire_json(empire_filepath, output_dir):
    """Splits empire.json into individual dealer files."""
    data = load_json(empire_filepath)
    if not data or not isinstance(data.get("dealers"), list):
        messagebox.showerror("Error", "Invalid empire.json structure. Missing 'dealers' array or it's not a list.")
        return

    if not os.path.exists(output_dir):
        try: os.makedirs(output_dir)
        except Exception as e: messagebox.showerror("Error", f"Could not create output directory: {output_dir}\n{e}"); return

    count, errors = 0, 0
    for dealer in data["dealers"]:
        if not isinstance(dealer, dict):
            print(f"Warning: Skipping non-dictionary item in dealers list during split."); errors += 1; continue
        dealer_name = dealer.get("name", "unnamed_dealer")
        filename = sanitize_filename(dealer_name) + ".json"
        filepath = os.path.join(output_dir, filename)
        if save_json(dealer, filepath): count += 1
        else: errors += 1

    if errors == 0: messagebox.showinfo("Split Complete", f"Successfully split {count} dealers into files in '{output_dir}'.")
    else: messagebox.showwarning("Split Complete with Errors", f"Split {count} dealers successfully.\nFailed to save or skipped {errors} items.")

def combine_dealer_files(dealer_filepaths, output_filepath):
    """Combines individual dealer files into empire.json."""
    all_dealers, errors, loaded_files = [], 0, 0
    for filepath in dealer_filepaths:
        dealer_data = load_json(filepath)
        if dealer_data:
            if isinstance(dealer_data, dict): all_dealers.append(dealer_data); loaded_files += 1
            else: messagebox.showwarning("Warning", f"Skipping file with unexpected structure (not a dictionary): {filepath}"); errors += 1
        else: errors += 1

    if not all_dealers: messagebox.showerror("Error", "No valid dealer data (dictionaries) found in selected files."); return
    empire_data = {"dealers": all_dealers}
    if save_json(empire_data, output_filepath):
        messagebox.showinfo("Combine Complete", f"Successfully combined {loaded_files} dealer files into '{output_filepath}'.\n{errors} files had errors or were skipped.")
    else: messagebox.showerror("Error", f"Failed to save the combined file to '{output_filepath}'.")


# --- GUI Application Class ---

class DDSModEditorApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Drug Dealer Simulator - Mod Editor")
        self.root.geometry("1000x800") # Increased size slightly

        # Data Storage
        self.empire_data = None
        self.current_filepath = None
        self.selected_tree_item_id = None
        self.tree_item_data_map = {} # Maps Treeview item ID -> associated data

        # Build UI
        self._setup_styles()
        self._build_ui()
        self.clear_editor_frame() # Initial placeholder

    def _setup_styles(self):
        """Applies ttk styles and themes."""
        self.style = ttk.Style()
        available_themes = self.style.theme_names()
        if 'clam' in available_themes: self.style.theme_use('clam')
        elif 'vista' in available_themes: self.style.theme_use('vista')
        elif 'aqua' in available_themes: self.style.theme_use('aqua')

    def _build_ui(self):
        """Creates the main UI elements."""
        main_frame = ttk.Frame(self.root, padding="10")
        main_frame.pack(fill=tk.BOTH, expand=True)

        # --- Top Buttons ---
        top_button_frame = ttk.Frame(main_frame)
        top_button_frame.pack(fill=tk.X, pady=(0, 10))
        button_configs = [
            ("Load empire.json", self.load_empire_file),
            ("Save empire.json", self.save_empire_file),
            ("Split Dealers", self.split_dealers),
            ("Combine Dealers", self.combine_dealers)
        ]
        for text, command in button_configs:
            ttk.Button(top_button_frame, text=text, command=command).pack(side=tk.LEFT, padx=5)

        # --- Main Paned Window ---
        paned_window = ttk.PanedWindow(main_frame, orient=tk.HORIZONTAL)
        paned_window.pack(fill=tk.BOTH, expand=True)

        # --- Left Pane (Tree View) ---
        left_pane = self._build_left_pane(paned_window)
        paned_window.add(left_pane, weight=2) # Adjusted weight

        # --- Right Pane (Editor) ---
        right_pane = self._build_right_pane(paned_window)
        paned_window.add(right_pane, weight=3) # Adjusted weight

        # --- Status Bar ---
        self.status_var = tk.StringVar(value="Ready. Load empire.json to begin.")
        status_bar = ttk.Label(self.root, textvariable=self.status_var, relief=tk.SUNKEN, anchor=tk.W)
        status_bar.pack(side=tk.BOTTOM, fill=tk.X)

    def _build_left_pane(self, parent):
        """Builds the left pane containing the Treeview and control buttons."""
        left_frame = ttk.Frame(parent, padding=5)
        left_frame.grid_rowconfigure(1, weight=1)
        left_frame.grid_columnconfigure(0, weight=1)

        ttk.Label(left_frame, text="Data Structure").grid(row=0, column=0, sticky="w", columnspan=2, pady=(0, 5))

        # Treeview setup
        self.tree = ttk.Treeview(left_frame, selectmode="browse", show="tree headings")
        self.tree.grid(row=1, column=0, sticky="nsew")
        tree_scrollbar_y = ttk.Scrollbar(left_frame, orient=tk.VERTICAL, command=self.tree.yview)
        tree_scrollbar_y.grid(row=1, column=1, sticky="ns")
        tree_scrollbar_x = ttk.Scrollbar(left_frame, orient=tk.HORIZONTAL, command=self.tree.xview)
        tree_scrollbar_x.grid(row=2, column=0, sticky="ew")
        self.tree.configure(yscrollcommand=tree_scrollbar_y.set, xscrollcommand=tree_scrollbar_x.set)
        self.tree.bind("<<TreeviewSelect>>", self.on_tree_select)

        # Control Buttons Frame (below tree)
        control_button_frame = ttk.Frame(left_frame)
        control_button_frame.grid(row=3, column=0, columnspan=2, sticky="ew", pady=(5, 0))
        ttk.Button(control_button_frame, text="Add Dealer", command=self.add_dealer).pack(side=tk.LEFT, padx=5)
        ttk.Button(control_button_frame, text="Remove Dealer", command=self.remove_dealer).pack(side=tk.LEFT, padx=5)
        ttk.Button(control_button_frame, text="Remove Item", command=self.remove_selected_tree_item).pack(side=tk.LEFT, padx=5)

        return left_frame

    def _build_right_pane(self, parent):
        """Builds the right pane containing the scrollable editor frame."""
        right_container = ttk.Frame(parent)
        right_canvas = tk.Canvas(right_container)
        right_scrollbar_y = ttk.Scrollbar(right_container, orient=tk.VERTICAL, command=right_canvas.yview)
        self.editor_frame = ttk.Frame(right_canvas, padding=10)
        self.editor_frame.bind("<Configure>", lambda e: right_canvas.configure(scrollregion=right_canvas.bbox("all")))
        right_canvas.create_window((0, 0), window=self.editor_frame, anchor="nw")
        right_canvas.configure(yscrollcommand=right_scrollbar_y.set)
        right_scrollbar_y.pack(side=tk.RIGHT, fill=tk.Y)
        right_canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        return right_container

    # --- File Operation Methods ---

    def load_empire_file(self):
        """Opens file dialog, loads JSON, populates tree."""
        filepath = filedialog.askopenfilename(title="Select empire.json", filetypes=[("JSON files", "*.json")])
        if not filepath: return
        data = load_json(filepath)
        if data and isinstance(data.get("dealers"), list):
            self.empire_data = data; self.current_filepath = filepath
            self.populate_tree(); self.clear_editor_frame()
            self.status_var.set(f"Loaded: {os.path.basename(filepath)}")
        elif data is not None:
            messagebox.showerror("Error", "Invalid empire.json: Missing 'dealers' list."); self.status_var.set("Invalid file structure.")

    def save_empire_file(self):
        """Saves current data, asking for path if needed."""
        if not self.empire_data: messagebox.showerror("Error", "No data loaded."); return
        save_path = self.current_filepath or filedialog.asksaveasfilename(title="Save As", defaultextension=".json", initialfile="empire.json")
        if not save_path: return
        if save_json(self.empire_data, save_path):
            self.current_filepath = save_path; self.status_var.set(f"Saved: {os.path.basename(save_path)}")
            messagebox.showinfo("Success", "File saved successfully.")
        else: self.status_var.set("Save failed.")

    def split_dealers(self):
        """Splits current data into dealer files."""
        if not self._ensure_data_loaded(): return
        output_dir = filedialog.askdirectory(title="Select Output Directory")
        if output_dir:
            split_empire_json(self.current_filepath, output_dir); self.status_var.set("Split attempted.")

    def combine_dealers(self):
        """Combines dealer files into a new empire structure."""
        filepaths = filedialog.askopenfilenames(title="Select Dealer Files", filetypes=[("JSON files", "*.json")])
        if not filepaths: return
        output_filepath = filedialog.asksaveasfilename(title="Save Combined As", defaultextension=".json", initialfile="empire.json")
        if not output_filepath: return
        combine_dealer_files(filepaths, output_filepath)
        self.status_var.set("Combine attempted.")
        if os.path.exists(output_filepath) and messagebox.askyesno("Load Combined File?", f"Load '{os.path.basename(output_filepath)}'?"):
             self._load_file_data(output_filepath) # Use helper to load

    def _ensure_data_loaded(self):
        """Checks if data is loaded, prompts to load if not. Returns True if data exists."""
        if self.empire_data: return True
        if messagebox.askyesno("Load File?", "No data loaded. Load empire.json first?"):
            self.load_empire_file()
            return bool(self.empire_data) # Return True if load was successful
        return False

    def _load_file_data(self, filepath):
        """Helper to load data from a specific filepath and update UI."""
        data = load_json(filepath)
        if data and isinstance(data.get("dealers"), list):
            self.empire_data = data; self.current_filepath = filepath
            self.populate_tree(); self.clear_editor_frame()
            self.status_var.set(f"Loaded: {os.path.basename(filepath)}")
            return True
        elif data is not None:
             messagebox.showerror("Error", "Invalid file structure."); self.status_var.set("Load failed: Invalid structure.")
        return False


    # --- Treeview Management Methods ---

    def populate_tree(self):
        """Clears and repopulates the treeview from self.empire_data."""
        self._clear_tree()
        if not self.empire_data or not isinstance(self.empire_data.get("dealers"), list): return
        for index, dealer in enumerate(self.empire_data["dealers"]):
            if isinstance(dealer, dict):
                dealer_name = dealer.get("name", f"Dealer {index+1}")
                # Insert top-level dealer node
                dealer_node_id = self.tree.insert("", tk.END, text=dealer_name, open=False, tags=('dealer',))
                # Map this node ID directly to the dealer dictionary
                self.tree_item_data_map[dealer_node_id] = dealer
                # Recursively populate children
                self._populate_node_recursive(dealer_node_id, dealer)

    def _clear_tree(self):
        """Clears the treeview and the data map."""
        for item in self.tree.get_children(): self.tree.delete(item)
        self.tree_item_data_map.clear()

    def _populate_node_recursive(self, parent_node_id, data_item):
        """Recursively populates tree nodes for dicts and lists."""
        # Map the node ID passed in (parent_node_id) to the data item it represents
        self.tree_item_data_map[parent_node_id] = data_item

        if isinstance(data_item, dict):
            # Parent node represents this dict. Create children for keys.
            for key, value in data_item.items():
                node_text = f"{key}:"
                # Child node represents the key; it will hold the value display or further children
                key_node_id = self.tree.insert(parent_node_id, tk.END, text=node_text, open=False)
                # Map this key node to the *dictionary it belongs to* (data_item), needed for context when editing value.
                self.tree_item_data_map[key_node_id] = data_item
                # Recurse with the value
                self._populate_node_recursive(key_node_id, value)

        elif isinstance(data_item, list):
            # Parent node represents this list. Tag it.
            self.tree.item(parent_node_id, tags=('list_holder',))
            for index, item in enumerate(data_item):
                 item_text = self._get_list_item_display_text(index, item)
                 # Create node for the list item
                 list_item_node_id = self.tree.insert(parent_node_id, tk.END, text=item_text, open=False)
                 # Map item node to the item data itself
                 self.tree_item_data_map[list_item_node_id] = item
                 # Recurse if the item is complex
                 if isinstance(item, (dict, list)):
                     self._populate_node_recursive(list_item_node_id, item)
                 else:
                     self.tree.item(list_item_node_id, tags=('simple_list_item',))

        else: # Simple value - Update the text of the parent node (which represents the key or list item)
            parent_node_id_text = self.tree.item(parent_node_id, "text")
            display_value = "None" if data_item is None else data_item
            # Append value only if it's a key node (ends with ':')
            if parent_node_id_text.endswith(':'):
                self.tree.item(parent_node_id, text=f"{parent_node_id_text} {display_value}")
            # Tag the parent node as holding a simple value
            current_tags = list(self.tree.item(parent_node_id, "tags"))
            # Determine if parent is key or list item based on grandparent data type
            grandparent_id = self.tree.parent(parent_node_id)
            grandparent_data = self.tree_item_data_map.get(grandparent_id)
            if isinstance(grandparent_data, list): # Parent is a list item node
                 if 'simple_list_item' not in current_tags: current_tags.append('simple_list_item')
            else: # Assume parent is a key node in a dict
                 if 'simple_value_holder' not in current_tags: current_tags.append('simple_value_holder')
            self.tree.item(parent_node_id, tags=tuple(current_tags))


    def _get_list_item_display_text(self, index, item):
        """Generates the display text for a list item in the tree."""
        item_text = f"[{index}]"
        display_value = "None" if item is None else item
        if isinstance(item, dict):
            name = item.get("name"); type_val = item.get("type")
            if name: item_text += f" {name}"
            elif type_val: item_text += f" {type_val}"
        elif not isinstance(item, (dict, list)): # Simple value
            item_text += f": {display_value}"
        return item_text

    def on_tree_select(self, event):
        """Handles selection changes in the treeview."""
        selected_ids = self.tree.selection()
        self.selected_tree_item_id = selected_ids[0] if selected_ids else None
        self.display_editor_for_selection()


    # --- Add/Remove Operations ---

    def add_dealer(self):
        """Adds a new dealer, allowing user to choose a template."""
        if not self._ensure_data_loaded(): return

        dealers = self.empire_data.get("dealers", [])
        template_options = { f"[{i}] {d.get('name', 'Unnamed')}": d for i, d in enumerate(dealers) if isinstance(d, dict)}
        options = [ADD_DEFAULT_OPTION] + list(template_options.keys())

        choice = self._ask_choice("Select Template Dealer", "Choose dealer to duplicate:", options)
        if not choice: return # User cancelled

        new_dealer = None
        if choice == ADD_DEFAULT_OPTION or not template_options:
             new_dealer = { "name": DEFAULT_NEW_DEALER_NAME, "image": "new.png", "dealTimes": [],
                            "dealTimesMult": [], "penalties": [], "unlockRequirements": [],
                            "drugs": [], "shipping": [], "dialogue": {} }
             for key in ["intro", "dealStart", "accept", "incomplete", "expire", "fail", "success", "reward"]:
                 new_dealer["dialogue"][key] = []
        else:
            template_dealer = template_options.get(choice)
            if template_dealer: new_dealer = create_new_item_from_template(template_dealer)

        if new_dealer:
            self.empire_data["dealers"].append(new_dealer)
            dealer_name = new_dealer.get("name", DEFAULT_NEW_DEALER_NAME)
            dealer_node_id = self.tree.insert("", tk.END, text=dealer_name, open=False, tags=('dealer',))
            self.tree_item_data_map[dealer_node_id] = new_dealer
            self._populate_node_recursive(dealer_node_id, new_dealer)
            self._select_and_focus_node(dealer_node_id)
            self.status_var.set("Added new dealer. Remember to Save.")

    def remove_dealer(self):
        """Removes the selected top-level dealer."""
        node_id = self.selected_tree_item_id
        if not node_id or 'dealer' not in self.tree.item(node_id, "tags"):
            messagebox.showwarning("Warning", "Select a top-level dealer name to remove.")
            return
        dealer_data = self.tree_item_data_map.get(node_id)
        if not dealer_data or dealer_data not in self.empire_data.get("dealers", []):
             messagebox.showerror("Error", "Data inconsistency. Cannot remove dealer."); return
        dealer_name = dealer_data.get("name", "this dealer")
        if messagebox.askyesno("Confirm Removal", f"Remove '{dealer_name}'?"):
            try:
                self.empire_data["dealers"].remove(dealer_data)
                self._remove_node_and_children_from_map(node_id)
                self.tree.delete(node_id)
                self.clear_editor_frame(); self.selected_tree_item_id = None
                self.status_var.set(f"Removed '{dealer_name}'. Remember to Save.")
            except ValueError: messagebox.showerror("Error", "Failed to remove dealer data.")
            except Exception as e: messagebox.showerror("Error", f"Error during removal:\n{e}")

    def remove_selected_tree_item(self):
        """Removes the selected item if it's a removable list item."""
        node_id = self.selected_tree_item_id
        if not node_id: messagebox.showwarning("Warning", "Select an item to remove."); return
        if 'dealer' in self.tree.item(node_id, "tags"): messagebox.showwarning("Cannot Remove", "Use 'Remove Dealer' button."); return
        parent_id = self.tree.parent(node_id)
        if not parent_id: messagebox.showwarning("Cannot Remove", "Cannot remove this item."); return

        # Get the actual list data from the parent node's mapping
        parent_data = self.tree_item_data_map.get(parent_id)
        # Ensure the parent node actually represents a list (check mapped data, not tag)
        if not isinstance(parent_data, list):
             # Maybe the parent is a key node whose value is a list? Check grandparent.
             grandparent_id = self.tree.parent(parent_id)
             grandparent_data = self.tree_item_data_map.get(grandparent_id)
             if isinstance(grandparent_data, dict):
                 key_text = self.tree.item(parent_id, "text").split(':', 1)[0].strip()
                 actual_list = grandparent_data.get(key_text)
                 if isinstance(actual_list, list):
                     parent_data = actual_list # Found the list!
                     # We need the node ID of the list holder (parent_id) for refresh later
                     list_holder_node_id = parent_id
                 else:
                      messagebox.showwarning("Cannot Remove", "Item not inside a list."); return
             else:
                  messagebox.showwarning("Cannot Remove", "Item not inside a list."); return
        else:
             # Parent node directly maps to the list
             list_holder_node_id = parent_id


        item_data = self.tree_item_data_map.get(node_id)
        item_index = self.find_key_or_index(parent_data, item_data) # Find index in the actual list

        if item_index is None: messagebox.showerror("Error", "Could not find item in list data."); return

        item_text = str(item_data)[:50]
        if messagebox.askyesno("Confirm Removal", f"Remove this list item?\nPreview: {item_text}..."):
            try:
                del parent_data[item_index] # Delete from actual list
                self._remove_node_and_children_from_map(node_id) # Clean map
                self.tree.delete(node_id) # Delete from tree
                self.refresh_tree_node_texts(list_holder_node_id) # Refresh sibling indices
                self._select_and_focus_node(list_holder_node_id) # Reselect parent list holder
                self.status_var.set("Removed item. Remember to Save.")
            except Exception as e: messagebox.showerror("Error", f"Error removing item:\n{e}")

    def _remove_node_and_children_from_map(self, node_id):
        """Recursively remove node and its children from the data map."""
        if node_id in self.tree_item_data_map: del self.tree_item_data_map[node_id]
        children = list(self.tree.get_children(node_id))
        for child_id in children:
            if self.tree.exists(child_id): self._remove_node_and_children_from_map(child_id)

    def _select_and_focus_node(self, node_id):
        """Selects, focuses, and ensures a tree node is visible."""
        if node_id and self.tree.exists(node_id):
            self.tree.selection_set(node_id); self.tree.focus(node_id); self.tree.see(node_id)


    # --- Editor Frame Management Methods ---

    def clear_editor_frame(self):
        """Removes all widgets from the editor frame."""
        for widget in self.editor_frame.winfo_children(): widget.destroy()
        ttk.Label(self.editor_frame, text="Select item in tree to edit.").pack(pady=20, padx=10)

    def display_editor_for_selection(self):
        """Populates the editor frame based on the selected tree item."""
        self.clear_editor_frame()
        node_id = self.selected_tree_item_id
        if not node_id: return

        node_tags = self.tree.item(node_id, "tags")
        node_text = self.tree.item(node_id, "text")
        # node_data is the data item *represented* by this node (dict, list, or simple value)
        node_data = self.tree_item_data_map.get(node_id)
        parent_id = self.tree.parent(node_id)
        # parent_data is the dict or list that *contains* the node_data (or is represented by parent node)
        parent_data = self.tree_item_data_map.get(parent_id)

        ttk.Label(self.editor_frame, text=f"Selected: {node_text}", font=("TkDefaultFont", 12, "bold")).pack(pady=(0, 10), anchor='w')

        # --- Determine Editor Type based on what the selected node REPRESENTS ---
        if 'dealer' in node_tags and isinstance(node_data, dict):
            self._build_dict_viewer(node_data) # View top-level dealer dict
        elif 'list_holder' in node_tags and isinstance(node_data, list):
            # Selected node represents a key/item holding a list. node_data is the list.
            self._build_list_editor(node_data) # Editor for the list itself
        elif 'simple_value_holder' in node_tags:
            # Selected node represents a key holding a simple value (e.g., "name: Walter")
            # node_data is the *containing dictionary*.
            container = node_data
            if isinstance(container, dict):
                try:
                    key = node_text.split(':', 1)[0].strip()
                    actual_value = container.get(key)
                    self._build_simple_value_editor(actual_value, container, key)
                except Exception as e: self._show_editor_error(f"Error parsing key/value: {e}")
            else: self._show_editor_error(f"Data mismatch for simple value holder. Expected dict, got {type(container).__name__}.")
        elif 'simple_list_item' in node_tags:
             # Selected node represents a simple value within a list (e.g., "[0]: 1.2")
             # node_data is the *simple value itself*. parent_data should be the *containing list*.
             container = parent_data # Parent node should map to the list
             actual_value = node_data
             if isinstance(container, list):
                 key_or_index = self.find_key_or_index(container, actual_value)
                 if key_or_index is not None:
                     self._build_simple_value_editor(actual_value, container, key_or_index)
                 else: self._show_editor_error("Cannot find simple list item index in parent list data.")
             else: # If parent_data isn't the list, maybe the grandparent is the container?
                  grandparent_id = self.tree.parent(parent_id)
                  grandparent_data = self.tree_item_data_map.get(grandparent_id)
                  if isinstance(grandparent_data, list): # e.g. list of lists
                      container = grandparent_data
                      key_or_index = self.find_key_or_index(container, actual_value)
                      if key_or_index is not None:
                           self._build_simple_value_editor(actual_value, container, key_or_index)
                      else: self._show_editor_error("Cannot find simple list item index in grandparent list data.")
                  else:
                      self._show_editor_error("Parent context is not a list for simple list item.")

        elif isinstance(node_data, dict):
             # Node represents a dictionary item within a list (e.g. "[0] Drug Name")
             self._build_dict_viewer(node_data)
        elif isinstance(node_data, list):
             # Node represents a list item within a list (e.g. "[1] [1, 2, 3]")
             self._build_list_editor(node_data)
        else:
             # Fallback if node type is unclear or not directly editable
             ttk.Label(self.editor_frame, text="Select a specific value or list node to edit.").pack()

    def _show_editor_error(self, message):
        """Displays an error message in the editor frame."""
        ttk.Label(self.editor_frame, text=f"Error: {message}", foreground="red").pack()

    # --- Editor Building Methods ---

    def _build_dict_viewer(self, data_dict):
        """Creates a read-only view for dictionary contents."""
        ttk.Label(self.editor_frame, text="Dictionary (Select child nodes to edit values):").pack(anchor='w', pady=(5,2))
        frame = ttk.Frame(self.editor_frame)
        frame.pack(fill=tk.BOTH, expand=True, padx=5)

        text_widget = tk.Text(frame, height=15, width=70, wrap=tk.WORD, relief=tk.FLAT, state=tk.DISABLED, borderwidth=0, highlightthickness=0)
        text_scroll = ttk.Scrollbar(frame, orient=tk.VERTICAL, command=text_widget.yview)
        text_widget.configure(yscrollcommand=text_scroll.set)
        text_scroll.pack(side=tk.RIGHT, fill=tk.Y)
        text_widget.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        text_widget.config(state=tk.NORMAL)
        text_widget.delete("1.0", tk.END)
        for key, value in data_dict.items():
            value_type = type(value).__name__
            line = f"- {key}: ({value_type})"
            if not isinstance(value, (dict, list)):
                 display_value = "None" if value is None else value
                 line += f" = {display_value}"
            text_widget.insert(tk.END, line + "\n")
        text_widget.config(state=tk.DISABLED)

    def _build_list_editor(self, data_list):
        """Creates editor widgets for managing a list (add item)."""
        list_frame = ttk.Frame(self.editor_frame)
        list_frame.pack(fill=tk.X, expand=True)
        ttk.Label(list_frame, text=f"List Items ({len(data_list)}): (Select items in tree to edit/view)").pack(anchor='w')
        button_frame = ttk.Frame(list_frame)
        button_frame.pack(fill=tk.X, pady=5)
        add_button = ttk.Button(button_frame, text="Add Item to This List",
                                command=lambda d=data_list: self.add_list_item(d))
        add_button.pack(side=tk.LEFT, padx=5)
        # Remove button is global

    def _build_simple_value_editor(self, value, data_container, key_or_index):
        """Creates an editor widget for a simple value."""
        value_frame = ttk.Frame(self.editor_frame)
        value_frame.pack(fill=tk.X, pady=5)
        value_frame.columnconfigure(1, weight=1)

        key_text = key_or_index if isinstance(data_container, dict) else f"Index [{key_or_index}]"
        ttk.Label(value_frame, text=f"{key_text}:").grid(row=0, column=0, padx=(0, 5), sticky='nsew')

        var, widget = None, None

        # --- Create widget based on value type ---
        if isinstance(value, bool):
            var = tk.BooleanVar(value=value)
            widget = ttk.Checkbutton(value_frame, variable=var, text="True/False",
                                     command=lambda v=var, c=data_container, k=key_or_index: self.update_simple_value(v.get(), c, k))
            widget.grid(row=0, column=1, sticky='w')
        elif isinstance(value, (int, float)):
            var = tk.StringVar(value=str(value))
            widget = ttk.Entry(value_frame, textvariable=var, width=40)
            widget.grid(row=0, column=1, sticky='ew')
        elif isinstance(value, str) and (len(value) > 80 or '\n' in value): # Multiline Text
             text_frame = ttk.Frame(value_frame); text_frame.grid(row=0, column=1, sticky='nsew')
             text_frame.columnconfigure(0, weight=1); text_frame.rowconfigure(0, weight=1)
             widget = tk.Text(text_frame, height=5, width=60, wrap=tk.WORD, undo=True)
             widget.grid(row=0, column=0, sticky='nsew'); widget.insert("1.0", value)
             scroll = ttk.Scrollbar(text_frame, orient=tk.VERTICAL, command=widget.yview)
             scroll.grid(row=0, column=1, sticky='ns'); widget.configure(yscrollcommand=scroll.set)
             save_btn = ttk.Button(text_frame, text="Save Text Changes", command=lambda w=widget, c=data_container, k=key_or_index: self.update_simple_value(w.get("1.0", tk.END).strip(), c, k))
             save_btn.grid(row=1, column=0, columnspan=2, pady=(5,0), sticky='w')
        else: # Single line string or None
            current_val_str = "" if value is None else str(value)
            var = tk.StringVar(value=current_val_str)
            widget = ttk.Entry(value_frame, textvariable=var, width=60)
            widget.grid(row=0, column=1, sticky='ew')
            if value is None: ttk.Label(value_frame, text="(None)").grid(row=0, column=2, padx=(5,0))

        # Bind updates for Entry widgets
        if isinstance(widget, ttk.Entry):
            widget.bind("<FocusOut>", lambda e, v=var, c=data_container, k=key_or_index, ov=value: self.update_simple_value(v.get(), c, k, original_value=ov))
            widget.bind("<Return>", lambda e, v=var, c=data_container, k=key_or_index, ov=value: self.update_simple_value(v.get(), c, k, original_value=ov))


    # --- Data Update Methods ---

    def add_list_item(self, data_list):
        """Adds a new item to the specified list, allowing template choice, and updates tree."""
        if not isinstance(data_list, list): messagebox.showerror("Error", "Target data is not a list."); return

        new_item = None
        contains_dicts = any(isinstance(item, dict) for item in data_list)

        if contains_dicts:
            template_options = { self._get_list_item_display_text(i, item): item
                                 for i, item in enumerate(data_list) if isinstance(item, dict)}
            options = [ADD_DEFAULT_OPTION] + list(template_options.keys())
            choice = self._ask_choice("Select Template", "Choose item to duplicate:", options)
            if not choice: return

            if choice == ADD_DEFAULT_OPTION or not template_options:
                template = next((item for item in data_list if isinstance(item, dict)), {})
                new_item = create_new_item_from_template(template)
            else:
                template_item = template_options.get(choice)
                if template_item: new_item = create_new_item_from_template(template_item)
        else: # Simple types or empty list
            template_item = data_list[0] if data_list else None
            new_item = create_new_item_from_template(template_item) if template_item is not None else DEFAULT_NEW_STRING

        if new_item is not None:
            data_list.append(new_item)
            # --- Update Tree ---
            list_holder_node_id = self._find_node_for_data(data_list, 'list_holder')
            if not list_holder_node_id:
                messagebox.showerror("Tree Sync Error", "Could not find the parent list node."); data_list.pop(); return

            index = len(data_list) - 1
            item_text = self._get_list_item_display_text(index, new_item)
            new_node_id = self.tree.insert(list_holder_node_id, tk.END, text=item_text, open=False)
            self.tree_item_data_map[new_node_id] = new_item # Map new item
            if isinstance(new_item, (dict, list)): self._populate_node_recursive(new_node_id, new_item)
            else: self.tree.item(new_node_id, tags=('simple_list_item',))

            self._select_and_focus_node(new_node_id)
            self.status_var.set("Added item. Remember to Save.")


    def update_simple_value(self, new_value_input, data_container, key_or_index, original_value=None):
        """Updates simple value in data and refreshes tree node text."""
        if data_container is None or key_or_index is None: return

        # Get original value if needed
        if original_value is None:
             try:
                 if isinstance(data_container, dict): original_value = data_container.get(key_or_index)
                 elif isinstance(data_container, list): original_value = data_container[key_or_index]
             except (IndexError, KeyError): messagebox.showerror("Error", "Cannot find original value."); return
             except Exception as e: messagebox.showerror("Error", f"Cannot get original value: {e}"); return

        target_type = type(original_value) if original_value is not None else None
        converted_value, error = self._convert_input_value(new_value_input, target_type)

        if error:
            messagebox.showerror("Invalid Input", f"{error}\nChanges not saved.")
            # Find the node being edited and refresh editor
            node_id_being_edited = self._find_node_for_value(data_container, key_or_index)
            if node_id_being_edited: self._select_and_focus_node(node_id_being_edited)
            return

        # --- Update data only if value has changed ---
        if converted_value != original_value:
            try:
                if isinstance(data_container, dict): data_container[key_or_index] = converted_value
                elif isinstance(data_container, list): data_container[key_or_index] = converted_value
                else: raise TypeError("Container not dict or list.")

                # Update Tree Text for the node representing the value (key or list item)
                value_node_id = self._find_node_for_value(data_container, key_or_index)
                if value_node_id:
                    self._update_tree_node_text(value_node_id, data_container, key_or_index, converted_value)
                    self.status_var.set(f"Updated value. Remember to Save.")
                else: self.status_var.set("Updated value (tree text might be stale). Remember to Save.")
            except IndexError: messagebox.showerror("Error", "Index out of range.")
            except Exception as e: messagebox.showerror("Error", f"Error during update:\n{e}")


    def _convert_input_value(self, input_val, target_type):
        """Converts input value to target type. Returns (value, error_message)."""
        try:
            str_input = str(input_val).strip()
            if isinstance(input_val, bool): return input_val, None
            if str_input.lower() == "none": return None, None
            if str_input.lower() == "true": return True, None
            if str_input.lower() == "false": return False, None
            if str_input == "" and target_type is not str: return None, None

            if target_type is bool: return None, "Invalid boolean (use true/false/none)"
            if target_type is int: return int(str_input), None
            if target_type is float: return float(str_input), None
            if target_type is str: return str(input_val), None

            # Guess type if original was None or unknown
            try: return int(str_input), None
            except ValueError:
                try: return float(str_input), None
                except ValueError: return str(input_val), None # Default to string
        except (ValueError, TypeError) as e:
            return None, f"Conversion to {target_type.__name__ if target_type else 'auto'} failed: {e}"


    def _find_node_for_data(self, data_item, required_tag=None):
        """Finds the first tree node ID mapped to the given data_item, optionally matching a tag."""
        for node_id, mapped_data in self.tree_item_data_map.items():
            if mapped_data is data_item: # Check identity
                 if required_tag is None or required_tag in self.tree.item(node_id, "tags"):
                     return node_id
        return None

    def _find_node_for_value(self, container, key_or_index):
         """Finds the tree node representing a specific value held by a container."""
         # Find the node representing the container first
         container_node_id = None
         # Check if container is a top-level dealer
         if isinstance(container, dict) and container in self.empire_data.get("dealers", []):
             container_node_id = self._find_node_for_data(container, 'dealer')
         else:
             # Otherwise, find the node mapped to this container (could be list holder or dict item)
             container_node_id = self._find_node_for_data(container)

         if not container_node_id:
              print(f"Warning: Could not find container node for {container}")
              return None # Cannot find container node

         # Now search children of the container node for the key/index
         for child_id in self.tree.get_children(container_node_id):
             if not self.tree.exists(child_id): continue # Skip deleted nodes
             child_text = self.tree.item(child_id, "text")
             if isinstance(container, dict):
                 # Match "key:" part
                 if child_text.startswith(str(key_or_index) + ":"):
                      # Check if this child node maps back to the container dict
                      # (This identifies it as the key node, not a node for a complex value)
                      if self.tree_item_data_map.get(child_id) is container:
                           return child_id
             elif isinstance(container, list):
                 # Match "[index]" or "[index]:" part
                 if child_text.startswith(f"[{key_or_index}]"):
                      # Check if this child node maps to the actual value at the index
                      try:
                          actual_value = container[key_or_index]
                          if self.tree_item_data_map.get(child_id) is actual_value:
                               return child_id
                      except IndexError: pass # Index might be temporarily invalid

         # Fallback: If the currently selected node seems correct, use it.
         # This helps if the editor was built directly for a simple value node.
         if self.selected_tree_item_id:
              tags = self.tree.item(self.selected_tree_item_id, "tags")
              # Check if selected node represents a simple value and its parent maps to the container
              parent_id = self.tree.parent(self.selected_tree_item_id)
              if ('simple_value_holder' in tags or 'simple_list_item' in tags) and \
                 self.tree_item_data_map.get(parent_id) is container:
                   # Further check if the text matches the key/index
                   selected_text = self.tree.item(self.selected_tree_item_id, "text")
                   if isinstance(container, dict) and selected_text.startswith(str(key_or_index) + ":"):
                        return self.selected_tree_item_id
                   elif isinstance(container, list) and selected_text.startswith(f"[{key_or_index}]"):
                        return self.selected_tree_item_id

         print(f"Warning: Could not reliably find node for value {key_or_index} in container.")
         return None # Node not reliably found


    def _update_tree_node_text(self, node_id, container, key_or_index, new_value):
        """Updates the display text of a specific tree node after its value changed."""
        if not self.tree.exists(node_id): return
        display_value = "None" if new_value is None else new_value
        new_text = ""
        if isinstance(container, dict):
            # Node represents "key: value"
            new_text = f"{key_or_index}: {display_value}"
            self.tree.item(node_id, text=new_text)
            # If the key was 'name', update the parent display text too
            if key_or_index == "name":
                parent_id = self.tree.parent(node_id)
                parent_data = self.tree_item_data_map.get(parent_id) # This is the dict item itself
                if parent_id and isinstance(parent_data, dict):
                     # Update top-level dealer node text
                     if 'dealer' in self.tree.item(parent_id, "tags"):
                          self.tree.item(parent_id, text=str(display_value))
                     else: # Update list item node text if parent is a dict in a list
                          grandparent_id = self.tree.parent(parent_id)
                          grandparent_data = self.tree_item_data_map.get(grandparent_id)
                          if grandparent_id and isinstance(grandparent_data, list):
                               g_index = self.find_key_or_index(grandparent_data, parent_data)
                               if g_index is not None:
                                    parent_text = self._get_list_item_display_text(g_index, parent_data)
                                    self.tree.item(parent_id, text=parent_text)

        elif isinstance(container, list):
            # Node represents "[index]: value" or "[index] Name/Type"
            new_text = self._get_list_item_display_text(key_or_index, new_value)
            self.tree.item(node_id, text=new_text)


    def refresh_tree_node_texts(self, start_node_id):
        """Recursively refreshes the text of nodes, useful after list modifications."""
        if not self.tree.exists(start_node_id): return
        tags = self.tree.item(start_node_id, "tags")
        node_data = self.tree_item_data_map.get(start_node_id)

        # --- Refresh List Item Indices/Text ---
        if 'list_holder' in tags and isinstance(node_data, list):
            child_nodes = self.tree.get_children(start_node_id)
            for index, child_id in enumerate(child_nodes):
                if not self.tree.exists(child_id): continue
                item_data = self.tree_item_data_map.get(child_id)
                new_text = self._get_list_item_display_text(index, item_data)
                if self.tree.item(child_id, "text") != new_text: self.tree.item(child_id, text=new_text)
                if isinstance(item_data, (dict, list)): self.refresh_tree_node_texts(child_id)

        # --- Refresh Simple Value Displays ---
        elif ('simple_value_holder' in tags or 'simple_list_item' in tags):
             container = None; key_or_index = None; value = None
             # Determine container, key/index, and value based on node type
             if 'simple_value_holder' in tags: # Key node
                  container = node_data # Mapped data is the dict
                  if isinstance(container, dict):
                      try: key_or_index = self.tree.item(start_node_id, "text").split(':', 1)[0].strip()
                      except: pass
                      if key_or_index is not None: value = container.get(key_or_index)
             elif 'simple_list_item' in tags: # List item node
                  value = node_data # Mapped data is the value
                  parent_id = self.tree.parent(start_node_id)
                  container = self.tree_item_data_map.get(parent_id)
                  if isinstance(container, list): key_or_index = self.find_key_or_index(container, value)

             if container is not None and key_or_index is not None:
                  self._update_tree_node_text(start_node_id, container, key_or_index, value) # Use helper

        # --- Recurse for other container types ---
        elif isinstance(node_data, dict) or 'dealer' in tags:
             children = list(self.tree.get_children(start_node_id))
             for child_id in children:
                 if self.tree.exists(child_id): self.refresh_tree_node_texts(child_id)

    # --- Helper Dialog ---
    def _ask_choice(self, title, prompt, options):
        """Shows a simple choice dialog using a Listbox. Returns the chosen option string or None."""
        if not options: return None

        dialog = tk.Toplevel(self.root)
        dialog.title(title)
        dialog.transient(self.root); dialog.grab_set(); dialog.resizable(False, False)
        ttk.Label(dialog, text=prompt, wraplength=300).pack(padx=10, pady=10)

        listbox_frame = ttk.Frame(dialog)
        listbox_frame.pack(padx=10, pady=5, fill=tk.BOTH, expand=True)
        listbox = tk.Listbox(listbox_frame, selectmode=tk.SINGLE, height=min(len(options), 15), width=60, exportselection=False) # Increased width/height
        scrollbar = ttk.Scrollbar(listbox_frame, orient=tk.VERTICAL, command=listbox.yview)
        listbox.configure(yscrollcommand=scrollbar.set)
        for option in options: listbox.insert(tk.END, option)
        listbox.selection_set(0); listbox.see(0)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        listbox.focus_set()

        result = None
        def on_ok(event=None):
            nonlocal result
            cur = listbox.curselection()
            result = listbox.get(cur[0]) if cur else options[0]
            dialog.destroy()
        def on_cancel(event=None): dialog.destroy()
        def on_double(event): on_ok()

        listbox.bind("<Double-Button-1>", on_double)
        dialog.bind("<Return>", on_ok); dialog.bind("<Escape>", on_cancel)

        button_frame = ttk.Frame(dialog); button_frame.pack(padx=10, pady=10, fill=tk.X)
        ok_button = ttk.Button(button_frame, text="OK", command=on_ok, default=tk.ACTIVE)
        ok_button.pack(side=tk.RIGHT, padx=5)
        cancel_button = ttk.Button(button_frame, text="Cancel", command=on_cancel)
        cancel_button.pack(side=tk.RIGHT)

        # Center dialog
        dialog.update_idletasks()
        x = self.root.winfo_rootx() + (self.root.winfo_width() // 2) - (dialog.winfo_width() // 2)
        y = self.root.winfo_rooty() + (self.root.winfo_height() // 3) - (dialog.winfo_height() // 2)
        dialog.geometry(f"+{x}+{y}")

        dialog.wait_window()
        return result


# --- Main Execution ---
if __name__ == "__main__":
    root = tk.Tk()
    app = DDSModEditorApp(root)
    root.mainloop()
