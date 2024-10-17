const canvas = document.getElementById('pixelCanvas');
const ctx = canvas.getContext('2d');

let gridSizeX = -1;
let gridSizeY = -1;
const pixelSize = 10;  // Size of each pixel
let scale = 1;  // Current zoom level
let translateX = 0;  // Current translation along X-axis
let translateY = 0;  // Current translation along Y-axis
let hoveredX = -1;  // Track the current hovered square X coordinate
let hoveredY = -1;  // Track the current hovered square Y coordinate
// Timer for pixel placement
let timeLeft = 0;  // Cooldown
let canPlacePixel = false;
let isLoggedIn = false;

canvas.width = window.innerWidth;
canvas.height = window.innerHeight;

// 2D color matrix to store drawn pixel colors
let colors = Array(0).fill().map(() => Array(0).fill('#FFFFFF'));

// Function to draw the grid
function drawGrid() {
    if (gridSizeX === -1) {
        return; // not initialized yet
    }
    // Reset transform to avoid accumulation of transformations
    ctx.resetTransform();

    // Clear the entire canvas area
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    // Apply current translation (panning) and scaling (zooming)
    ctx.translate(translateX, translateY);
    ctx.scale(scale, scale);

    // Draw the filled pixels
    for (let i = 0; i < gridSizeX; i++) {
        for (let j = 0; j < gridSizeY; j++) {
            // Draw the colored pixels from the color matrix
            ctx.fillStyle = colors[i][j];
            let sizeX = pixelSize + 0.01, sizeY = pixelSize + 0.01;
            let startX = i * pixelSize, startY = j * pixelSize;
            if (i == 0) {
                startX -= 0.1;
                sizeX += 0.1;
            }
            if (j == 0) {
                startY -= 0.1;
                sizeY += 0.1;
            }
            ctx.fillRect(startX, startY, sizeX, sizeY);
        }
    }

    // Draw the outline
    ctx.strokeStyle = '#000';  // Black grid lines
    ctx.lineWidth = 0.5;       // Thin lines
    ctx.strokeRect(-ctx.lineWidth / 2, -ctx.lineWidth / 2, gridSizeX * pixelSize + ctx.lineWidth, gridSizeY * pixelSize + ctx.lineWidth);

    // Highlight the currently hovered square with a yellow border
    let highlightWidth = 1;  // Width of the highlight border
    if (hoveredX >= 0 && hoveredY >= 0 && canPlacePixel && isLoggedIn) {
        ctx.strokeStyle = 'yellow';
        ctx.lineWidth = highlightWidth;
        ctx.strokeRect(hoveredX * pixelSize + highlightWidth / 2, hoveredY * pixelSize + highlightWidth / 2, pixelSize - highlightWidth, pixelSize - highlightWidth);

        ctx.lineWidth = 0;
        let selectedColor = document.getElementById('color-picker').value;
        ctx.fillStyle = selectedColor + '80';  // Transparent color
        ctx.fillRect(hoveredX * pixelSize + highlightWidth, hoveredY * pixelSize + highlightWidth, pixelSize - 2 * highlightWidth, pixelSize - 2 * highlightWidth);
    }
}

// Variables to handle panning
let isPanning = false;
let startX = 0;
let startY = 0;

// Handle right-click for panning (moving the grid)
canvas.addEventListener('mousedown', (e) => {
    if (e.button === 2) {  // Right-click
        isPanning = true;
        startX = e.clientX - translateX;
        startY = e.clientY - translateY;
    }
});

canvas.addEventListener('mousemove', (e) => {
    if (isPanning) {
        translateX = e.clientX - startX;
        translateY = e.clientY - startY;
        drawGrid();
    } else {
        // Handle hover and highlighting
        const rect = canvas.getBoundingClientRect();
        const x = Math.floor((e.clientX - rect.left - translateX) / (pixelSize * scale));
        const y = Math.floor((e.clientY - rect.top - translateY) / (pixelSize * scale));

        if (x >= 0 && x < gridSizeX && y >= 0 && y < gridSizeY) {
            hoveredX = x;
            hoveredY = y;
        } else {
            hoveredX = -1;
            hoveredY = -1;
        }

        drawGrid();  // Redraw the grid with the new highlight
    }
});

canvas.addEventListener('mouseup', () => {
    isPanning = false;
});

// Disable right-click context menu
canvas.addEventListener('contextmenu', (e) => e.preventDefault());

// Handle zooming with mouse wheel
canvas.addEventListener('wheel', (e) => {
    e.preventDefault();

    const zoomFactor = 1.1;

    // Get the current mouse position on the canvas
    const mouseX = e.clientX - canvas.getBoundingClientRect().left;
    const mouseY = e.clientY - canvas.getBoundingClientRect().top;

    // Get the world coordinates of the mouse pointer (before zoom)
    const worldX = (mouseX - translateX) / scale;
    const worldY = (mouseY - translateY) / scale;

    // Adjust scale (zoom in or out)
    if (e.deltaY > 0) {
        scale /= zoomFactor;  // Zoom out
    } else {
        scale *= zoomFactor;  // Zoom in
    }

    if (scale < 0.5) scale = 0.5;  // Minimum scale
    if (scale > 200) scale = 200;      // Maximum scale

    // Recalculate the translation to keep the mouse position stable
    translateX = mouseX - worldX * scale;
    translateY = mouseY - worldY * scale;

    drawGrid();  // Redraw after zooming
});

window.addEventListener('resize', () => {
    canvas.width = window.innerWidth;
    canvas.height = window.innerHeight;
    drawGrid(); // Redraw the grid after resizing
});

function updateTimerText() {
    const timerDisplay = document.getElementById('timer');
    const minutes = Math.floor(timeLeft / 60);
    const seconds = timeLeft % 60;
    if (timeLeft > 0) {
        timerDisplay.textContent = `Next Pixel: ${minutes}:${seconds < 10 ? '0' : ''}${seconds}`;
        canPlacePixel = false;  // Cannot place pixel during cooldown
    } else {
        timerDisplay.textContent = "Next Pixel: Ready";
        canPlacePixel = true;  // Allow pixel placement when timer reaches 0
        drawGrid();  // Redraw the grid to add the highlight
    }
}
function updateTimer() {
    timeLeft = Math.max(0, timeLeft - 1);  // Decrease time left by 1 second
    updateTimerText();
}

setInterval(updateTimer, 1000);  // Update timer every second

// Display cooldown notification
function showCooldownNotification() {
    const notification = document.getElementById('cooldown-notification');
    notification.style.display = 'block';
    setTimeout(() => {
        notification.style.display = 'none';
    }, 2000);  // Hide after 2 seconds
}

// Login/Logout button logic
const loginBtn = document.getElementById('login-btn');
const logoutBtn = document.getElementById('logout-btn');
const loginFormContainer = document.getElementById('login-form-container');
const loginSubmitBtn = document.getElementById('login-submit');
const closeLoginFormBtn = document.getElementById('close-login-form');
const displayUsername = document.getElementById('display-username');
const timerDisplay = document.getElementById('timer');

// Handle login button click - Show the login form
loginBtn.addEventListener('click', () => {
    loginFormContainer.style.display = 'block';  // Show login form
});

// Close the login form when the "X" button is clicked
closeLoginFormBtn.addEventListener('click', () => {
    loginFormContainer.style.display = 'none';  // Hide login form
    // Clear username and password fields after successful login
    document.getElementById('username').value = '';
    document.getElementById('password').value = '';
});

async function logIn(username, token) {
    console.log(`Logged in with token: ${token}`);
    loginFormContainer.style.display = 'none';  // Hide login form
    loginBtn.style.display = 'none';
    logoutBtn.style.display = 'block';
    timerDisplay.style.display = 'block';

    // Store token in local storage (you can store it more securely later)
    localStorage.setItem('authToken', token);
    localStorage.setItem('username', username);

    var userInfo = await getUserInfo(username, token); // Json object containing firstName and lastName
    var firstName = userInfo.firstName;
    var lastName = userInfo.lastName;
    var fullName = `${firstName} ${lastName}`;
    console.log(`Logged in as: ${fullName}`);
    localStorage.setItem('fullName', fullName);

    showLoggedInMessage(fullName);  // Show notification with username (optional)

    // Display the username next to the logout button
    displayUsername.textContent = `Logged in as: ${fullName}`;
    displayUsername.style.display = 'inline-block';

    isLoggedIn = true;
}

// Handle login form submission
loginSubmitBtn.addEventListener('click', async () => {
    const username = document.getElementById('username').value;
    const password = document.getElementById('password').value;

    // Validate that both fields are non-empty
    if (!username || !password) {
        showEmptyFieldsMessage();  // Show notification instead of alert
        return;
    }

    // Call the loginUser helper method and get the token
    const token = await loginUser(username, password);


    if (token) {
        await logIn(username, token);
        // Clear username and password fields after successful login
        document.getElementById('username').value = '';
        document.getElementById('password').value = '';
    }
});

function logOut() {
    console.log('User logged out');
    loginBtn.style.display = 'block';
    logoutBtn.style.display = 'none';
    timerDisplay.style.display = 'none';

    // Clear the token and username from local storage
    localStorage.removeItem('authToken');
    localStorage.removeItem('username');

    // Hide the displayed username
    displayUsername.style.display = 'none';

    isLoggedIn = false;
}

// Logout button click
logoutBtn.addEventListener('click', async () => {
    const username = localStorage.getItem('username');
    const token = localStorage.getItem('authToken');

    if (username && token) {
        const success = await logoutUser(username, token);

        if (success) {
            logOut();
        }
    }
});

// Function to handle session expiration, show notification, and log out user
function sessionExpired() {
    const notification = document.getElementById('session-expired');
    notification.style.display = 'block';
    setTimeout(() => {
        notification.style.display = 'none';
    }, 2000);  // Hide after 2 seconds
    logOut();
}

const openCreateUserBtn = document.getElementById('open-create-user-form');
const createUserFormContainer = document.getElementById('create-user-form-container');
const closeCreateUserFormBtn = document.getElementById('close-create-user-form');

openCreateUserBtn.addEventListener('click', () => {
    loginFormContainer.style.display = 'none';
    createUserFormContainer.style.display = 'block';
    document.getElementById('username').value = '';
    document.getElementById('password').value = '';
});

closeCreateUserFormBtn.addEventListener('click', () => {
    createUserFormContainer.style.display = 'none';
    document.getElementById('first-name').value = '';
    document.getElementById('last-name').value = '';
    document.getElementById('new-username').value = '';
    document.getElementById('new-password').value = '';
    document.getElementById('confirm-password').value = '';
});

const createUserSubmitBtn = document.getElementById('create-user-submit');
createUserSubmitBtn.addEventListener('click', async () => {
    const firstName = document.getElementById('first-name').value;
    const lastName = document.getElementById('last-name').value;
    const newUsername = document.getElementById('new-username').value;
    const newPassword = document.getElementById('new-password').value;
    const confirmPassword = document.getElementById('confirm-password').value;

    // Basic validation
    if (!firstName || !lastName || !newUsername || !newPassword || !confirmPassword) {
        showEmptyFieldsMessage();  // Notify user about empty fields
        return;
    }
    if (newPassword !== confirmPassword) {
        showPasswordMismatchMessage();  // Notify user about password mismatch
        return;
    }

    // Call createUser helper method and get the result
    const result = await createUser(firstName, lastName, newUsername, newPassword);

    if (result.success) {
        showUserCreatedMessage();  // Show notification

        createUserFormContainer.style.display = 'none';
        // clear the fields after successful user creation
        document.getElementById('first-name').value = '';
        document.getElementById('last-name').value = '';
        document.getElementById('new-username').value = '';
        document.getElementById('new-password').value = '';
        document.getElementById('confirm-password').value = '';
    }
});

let timestamp = -1;
async function updateTime() {
    try {
        const username = localStorage.getItem('username');
        const token = localStorage.getItem('authToken');
        if (!username || !token) {
            return;
        }
        let response = await fetch(`/remainingCooldown?username=${encodeURIComponent(username)}&token=${encodeURIComponent(token)}`, {
            method: 'GET'
        });
        if (!response.ok) {
            // Check if response is Unauthorized (401) and show session expired message
            if (response.status === 401) {
                sessionExpired();
                return;
            }
            else {
                console.error('Failed to get remaining cooldown', response);
            }
            throw new Error('Failed to fetch remaining cooldown');
        }
        let remainingCooldown = await response.json();
        if (Math.abs(timeLeft - remainingCooldown) > 1) {
            // Update the timer only if the difference is more than 1 second
            timeLeft = remainingCooldown;
            updateTimerText();
        }
    } catch (error) {
    }
}
async function updateGridAndTime() {
    try {
        let response;
        if (!timestamp) {
            timestamp = -1;
        }
        if (timestamp === -1) {
            response = await fetch('/getGrid', {
                method: 'GET'
            });
        }
        else {
            response = await fetch(`/getUpdatedCells?unixTimeSeconds=${timestamp}`, {
                method: 'GET'
            });
        }
        if (!response.ok) {
            console.error('Failed to get updated cells', response);
            throw new Error('Failed to update colors');
        }
        const data = await response.json();
        // Data is a class that has a Dictionary<string, string> called colors, and a long called lastUpdate. Each key in the dictionary is a string of the form "x,y" and the value is the color of the pixel at that position.
        // The dictionary represents the cells that have been updated since the last time the client checked.
        // The long represents the time of the last update.
        if (gridSizeX === -1) {
            // Initialize the grid size
            for (const key in data.colors) {
                const [x, y] = key.split(',').map(Number);
                gridSizeX = Math.max(gridSizeX, x + 1);
                gridSizeY = Math.max(gridSizeY, y + 1);
            }
            colors = Array(gridSizeX).fill().map(() => Array(gridSizeY).fill('#FFFFFF'));
        }
        timestamp = data.lastUpdate;
        let count = 0;
        for (const key in data.colors) {
            const [x, y] = key.split(',').map(Number);
            let color = data.colors[key];
            colors[x][y] = color;
            count++;
        }
        console.log(`updated grid at ${timestamp}, updated ${count} colors`);
        drawGrid();
    } catch (error) {
    }
    await updateTime();
}

// Call drawGrid at the start of the program to display the grid initially
updateGridAndTime()

// Call updateGridAndTime every 5 seconds to update the grid and time
setInterval(updateGridAndTime, 5000);

// Adjust click logic to take into account panning and zooming
canvas.addEventListener('click', async (e) => {
    if (!isLoggedIn) {
        const notification = document.getElementById('please-login');
        notification.style.display = 'block';
        setTimeout(() => {
            notification.style.display = 'none';
        }, 2000);  // Hide after 2 seconds
        return;
    }
    if (!canPlacePixel) {
        // Show the notification that the user needs to wait
        showCooldownNotification();
        return;
    }
    // Adjust the click coordinates based on the current scale and translation
    const rect = canvas.getBoundingClientRect();  // Get canvas position
    const x = Math.floor((e.clientX - rect.left - translateX) / (pixelSize * scale));
    const y = Math.floor((e.clientY - rect.top - translateY) / (pixelSize * scale));
    if (x >= 0 && x < gridSizeX && y >= 0 && y < gridSizeY) {
        let selectedColor = document.getElementById('color-picker').value;

        const username = localStorage.getItem('username');
        const token = localStorage.getItem('authToken');
        if (!username || !token) {
            sessionExpired();
            return;
        }
        const response = await fetch(`/place?username=${encodeURIComponent(username)}&token=${encodeURIComponent(token)}&x=${x}&y=${y}&color=${encodeURIComponent(selectedColor)}`, {
            method: 'POST'
        });
        if (!response.ok) {
            // Check if response is Unauthorized (401) and show session expired message
            if (response.status === 401) {
                sessionExpired();
                return;
            }
            else {
                // check if message is "Cooldown not expired" and show cooldown notification"
                const message = await response.text();
                if (message === "Cooldown not expired") {
                    showCooldownNotification();
                    return;
                }
                console.error('Failed to place pixel', response);
            }
            return;
        }
        colors[x][y] = selectedColor;  // Store color in matrix

        await updateTime();

        drawGrid();  // Redraw the grid after placing the pixel
    }
});

// At the start of the program, delete authToken and username if they exist
localStorage.removeItem('authToken');
localStorage.removeItem('username');