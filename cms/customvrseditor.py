import ScriptureObjects
import os
import subprocess

selectedProject = ScriptureObjects.ScriptureText(Project)
projectDir = selectedProject.directory

vrsPath = os.path.join(projectDir, "custom.vrs")
exePath = os.path.abspath(os.path.join(projectDir, "..", "cms", "CustomVrsEditorCms.exe"))

if not os.path.exists(exePath):
    print("CustomVrsEditorCms.exe not found at: " + exePath)
else:
    subprocess.Popen([exePath, vrsPath])
