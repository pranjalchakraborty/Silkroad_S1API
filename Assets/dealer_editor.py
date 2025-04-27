import json
import tkinter as tk
from tkinter import filedialog, messagebox, scrolledtext

# Global variable to store the merged JSON
merged_data = {"dealers": []}

def load_json_file():
    """Open a file dialog to load a JSON file and return its contents."""
    filename = filedialog.askopenfilename(filetypes=[("JSON files", "*.json")])
    if not filename:
        return None
    try:
        with open(filename, 'r', encoding='utf-8') as f:
            data = json.load(f)
        return data
    except Exception as e:
        messagebox.showerror("Error", f"Failed to load JSON: {e}")
        return None

def merge_dealers(json1, json2):
    """
    Merge two JSON objects having the structure:
      {"dealers": [ { "name": ... , ... }, ... ]}
    Merging is based on matching dealer names.
    """
    merged = {}
    # Merge each dealer by dealer name
    for source in (json1, json2):
        if not source or "dealers" not in source:
            continue
        for dealer in source["dealers"]:
            name = dealer.get("name")
            if not name:
                continue
            if name in merged:
                # Merge keys: update keys from dealer into existing record.
                # (Keys from this dealer override if conflict.)
                merged[name].update(dealer)
            else:
                merged[name] = dealer.copy()
    # Return merged data as a list under "dealers"
    return {"dealers": list(merged.values())}

def load_files():
    """Load two JSON files and merge them based on dealer name."""
    json1 = load_json_file()
    if json1 is None:
        return
    json2 = load_json_file()
    if json2 is None:
        return

    global merged_data
    merged_data = merge_dealers(json1, json2)
    refresh_dealer_list()
    messagebox.showinfo("Success", "Files merged successfully!")

def refresh_dealer_list():
    """Refresh the dealer listbox with names from the merged data."""
    dealer_listbox.delete(0, tk.END)
    for dealer in merged_data.get("dealers", []):
        name = dealer.get("name", "<No Name>")
        dealer_listbox.insert(tk.END, name)
    details_text.delete(1.0, tk.END)  # Clear details

def on_dealer_select(event):
    """Display selected dealer details in the text widget."""
    if not dealer_listbox.curselection():
        return
    index = dealer_listbox.curselection()[0]
    dealer = merged_data["dealers"][index]
    # Pretty-print the dealer JSON (indentation can help visualize nesting)
    details_text.delete(1.0, tk.END)
    details_text.insert(tk.END, json.dumps(dealer, indent=2))

def delete_dealer():
    """Delete the selected dealer from the merged data."""
    if not dealer_listbox.curselection():
        messagebox.showwarning("Warning", "No dealer selected for deletion.")
        return
    index = dealer_listbox.curselection()[0]
    dealer_name = merged_data["dealers"][index].get("name", "<No Name>")
    if messagebox.askyesno("Delete", f"Delete dealer '{dealer_name}'?"):
        del merged_data["dealers"][index]
        refresh_dealer_list()

def save_merged_json():
    """Save the merged JSON to a file."""
    filename = filedialog.asksaveasfilename(defaultextension=".json",
                                            filetypes=[("JSON files", "*.json")])
    if not filename:
        return
    try:
        # Try to validate the edited JSON from details_text if a dealer is selected.
        if dealer_listbox.curselection():
            index = dealer_listbox.curselection()[0]
            # Get the edited details (if any) and update the record before saving.
            edited_text = details_text.get(1.0, tk.END).strip()
            # Update the dealer entry only if valid JSON.
            try:
                updated_dealer = json.loads(edited_text)
                merged_data["dealers"][index] = updated_dealer
            except Exception as e:
                # If parsing fails, show a warning and do not update.
                messagebox.showwarning("Warning", "Edited dealer details are not valid JSON. Saving original data.")
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump(merged_data, f, indent=2)
        messagebox.showinfo("Success", "JSON saved successfully!")
    except Exception as e:
        messagebox.showerror("Error", f"Failed to save JSON: {e}")

# Create the GUI
root = tk.Tk()
root.title("Dealer JSON Merger/Editor")
root.geometry("800x600")

# Top frame for file operations
top_frame = tk.Frame(root)
top_frame.pack(fill=tk.X, padx=10, pady=5)

load_button = tk.Button(top_frame, text="Load & Merge JSON Files", command=load_files)
load_button.pack(side=tk.LEFT, padx=5)

save_button = tk.Button(top_frame, text="Save Merged JSON", command=save_merged_json)
save_button.pack(side=tk.LEFT, padx=5)

# Middle frame for dealer list and details
mid_frame = tk.Frame(root)
mid_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)

# Left: listbox for dealer names
dealer_frame = tk.Frame(mid_frame)
dealer_frame.pack(side=tk.LEFT, fill=tk.Y)

tk.Label(dealer_frame, text="Dealers:").pack()
dealer_listbox = tk.Listbox(dealer_frame, width=25)
dealer_listbox.pack(side=tk.LEFT, fill=tk.Y, padx=5, pady=5)
dealer_listbox.bind("<<ListboxSelect>>", on_dealer_select)

delete_button = tk.Button(dealer_frame, text="Delete Selected Dealer", command=delete_dealer)
delete_button.pack(pady=5)

# Right: text widget for dealer details (editable)
details_frame = tk.Frame(mid_frame)
details_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=10)
tk.Label(details_frame, text="Dealer Details (editable):").pack()

details_text = scrolledtext.ScrolledText(details_frame, wrap=tk.WORD)
details_text.pack(fill=tk.BOTH, expand=True)

# Information note
note = tk.Label(root, text="Note: The JSON shown may be edited directly. Keys and structure are not protected.", fg="gray")
note.pack(pady=2)

root.mainloop()
