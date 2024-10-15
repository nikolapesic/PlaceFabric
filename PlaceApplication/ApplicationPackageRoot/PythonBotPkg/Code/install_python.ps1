$pythonInstallPath = "C:\Program Files\Python310"
$pipInstallPath = "$pythonInstallPath\Scripts"
$pythonPath = "$pythonInstallPath\python.exe"

if (-not (Test-Path $pythonPath)) {
    # Python is not installed, download the Python installer
    $pythonInstallerPath = "$PSScriptRoot\python-3.10.4-amd64.exe"
    if (-not (Test-Path $pythonInstallerPath)) {
        Write-Host "Downloading Python installer..."
        Invoke-WebRequest -Uri "https://www.python.org/ftp/python/3.10.4/python-3.10.4-amd64.exe" -OutFile $pythonInstallerPath
    }
    
    # Install Python silently
    Write-Host "Installing Python..."
    Start-Process -FilePath $pythonInstallerPath -ArgumentList "/quiet InstallAllUsers=1 PrependPath=1" -Wait
    
    # Add Python to PATH for this session
    if (-not (Test-Path $pythonInstallPath)) {
        Write-Error "Python installation failed or not found in expected directory."
        exit 1
    }
    
    # Update PATH environment variable for the current session
    [System.Environment]::SetEnvironmentVariable("PATH", $Env:PATH + ";$pythonInstallPath;$pipInstallPath", [System.EnvironmentVariableTarget]::Process)
    
    Write-Host "Python and pip installed successfully, and PATH updated."
} else {
    Write-Host "Python is already installed."
}

# Ensure pip is installed
$pipPath = "$pipInstallPath\pip.exe"
Write-Host "Checking for pip... at $pipPath"
if (-not (Test-Path $pipPath)) {
    Write-Host "pip not found, installing pip..."
    # Download get-pip.py to install pip
    Invoke-WebRequest -Uri "https://bootstrap.pypa.io/get-pip.py" -OutFile "$PSScriptRoot\get-pip.py"
    $process = Start-Process -FilePath $pythonpath -ArgumentList "$PSScriptRoot\get-pip.py" -PassThru
    $process.WaitForExit()
} else {
    Write-Host "pip is already installed."
}

# Install the 'requests' library (or other dependencies) using pip
Write-Host "Installing Python dependencies..."
pip install requests

# Verify Python installation and dependencies
python --version
pip show requests
