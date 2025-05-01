import json
import os
from collections import defaultdict
from PyQt5.QtWidgets import QMessageBox, QInputDialog

# Add these methods to the JSONEditor class
class JSONEditor(QMainWindow):
    # ... existing code ...
    
    def init_ui(self):
        # ... existing file controls ...
        
        # Add new buttons
        self.btn_merge = QPushButton('Merge JSONs', self)
        self.btn_merge.clicked.connect(self.merge_jsons)
        self.btn_split = QPushButton('Split Dealers', self)
        self.btn_split.clicked.connect(self.split_dealers)
        
        file_layout.addWidget(self.btn_merge)
        file_layout.addWidget(self.btn_split)
        
        # Rest of existing init code...
    
    def merge_jsons(self):
        files, _ = QFileDialog.getOpenFileNames(self, 'Select JSONs to Merge', 
                                              '', 'JSON Files (*.json)')
        if not files:
            return
            
        merged_dealers = defaultdict(dict)
        
        # First load current data
        if self.data and 'dealers' in self.data:
            for dealer in self.data['dealers']:
                merged_dealers[dealer['name']] = dealer
                
        # Merge new files
        for file_path in files:
            try:
                with open(file_path, 'r') as f:
                    data = json.load(f)
                    if 'dealers' not in data:
                        continue
                        
                    for dealer in data['dealers']:
                        merged_dealers[dealer['name']] = dealer
            except Exception as e:
                QMessageBox.warning(self, 'Merge Error', 
                                  f'Failed to merge {os.path.basename(file_path)}:\n{str(e)}')
                
        self.data = {'dealers': list(merged_dealers.values())}
        self.schema = SchemaGenerator.infer_schema(self.data)
        self.build_ui()
        QMessageBox.information(self, 'Merge Complete',
                               f'Merged {len(files)} files\nTotal dealers: {len(self.data["dealers"])}')
    
    def split_dealers(self):
        if not self.data or 'dealers' not in self.data:
            QMessageBox.warning(self, 'Split Error', 'No dealers loaded!')
            return
            
        save_dir = QFileDialog.getExistingDirectory(self, 'Select Save Directory')
        if not save_dir:
            return
            
        created_files = []
        for dealer in self.data['dealers']:
            try:
                filename = f"{dealer['name'].replace(' ', '_')}.json"
                file_path = os.path.join(save_dir, filename)
                
                # Handle duplicates
                if os.path.exists(file_path):
                    overwrite = QMessageBox.question(self, 'File Exists',
                                                    f'Overwrite {filename}?',
                                                    QMessageBox.Yes | QMessageBox.No)
                    if overwrite != QMessageBox.Yes:
                        continue
                
                with open(file_path, 'w') as f:
                    json.dump({'dealers': [dealer]}, f, indent=2)
                    created_files.append(filename)
            except Exception as e:
                QMessageBox.warning(self, 'Split Error',
                                  f'Failed to save {filename}:\n{str(e)}')
                
        QMessageBox.information(self, 'Split Complete',
                              f'Saved {len(created_files)} dealer files\n'
                              f'Directory: {save_dir}')
    
    # Add to DataManager class
    class DataManager:
        # ... existing code ...
        
        def merge_data(self, new_data):
            """Merge new data into existing dealers"""
            existing_names = {d['name'] for d in self.data['dealers']}
            
            for dealer in new_data.get('dealers', []):
                if dealer['name'] in existing_names:
                    # Replace existing dealer
                    index = next(i for i,d in enumerate(self.data['dealers'])
                               if d['name'] == dealer['name'])
                    self.data['dealers'][index] = dealer
                else:
                    self.data['dealers'].append(dealer)