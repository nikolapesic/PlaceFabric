// Helper method to handle user login
async function loginUser(username, password) {
    try {
        const response = await fetch(`/login?username=${encodeURIComponent(username)}&password=${encodeURIComponent(password)}`, {
            method: 'POST'
        });

        if (!response.ok) {
            throw new Error('Login failed');
        }

        const token = await response.json();
        return token;  // Return the token for further use
    } catch (error) {
        showLoginFailedMessage();
        return null;
    }
}

async function getUserInfo(username, token) {
    try {
        const response = await fetch(`/userInfo?username=${encodeURIComponent(username)}&token=${encodeURIComponent(token)}`, {
            method: 'GET'
        });

        if (!response.ok) {
            throw new Error('Failed to get user info');
        }

        return await response.json();  // Return the user info
    } catch (error) {
        console.error("Failed to get user info: ", error);
        return null;
    }
}

// Helper method to handle user logout
async function logoutUser(username, token) {
    try {
        const response = await fetch(`/logout?username=${encodeURIComponent(username)}&token=${encodeURIComponent(token)}`, {
            method: 'POST'
        });

        if (!response.ok) {
            throw new Error('Logout failed');
        }

        return true;  // Logout succeeded
    } catch (error) {
        console.error("Logout failed: ", error);
        return false;
    }
}

// Helper method to check if user is authenticated
async function isAuthenticated(username, token) {
    try {
        const response = await fetch(`/isAuthenticated?username=${encodeURIComponent(username)}&token=${encodeURIComponent(token)}`, {
            method: 'GET'
        });

        if (!response.ok) {
            throw new Error('Session expired');
        }

        return await response.json();  // Return true if authenticated, false otherwise
    } catch (error) {
        showSessionExpiredMessage();
        return false;
    }
}

// Helper method to show login failed message
function showLoginFailedMessage() {
    const notification = document.getElementById('login-failed');
    notification.style.display = 'block';
    setTimeout(() => {
        notification.style.display = 'none';
    }, 2000);  // Hide after 2 seconds
}

// Helper method to handle user creation and login
async function createUser(firstName, lastName, username, password) {
    try {
        // Try to create the user
        const createResponse = await fetch(`/createUser?firstName=${encodeURIComponent(firstName)}&lastName=${encodeURIComponent(lastName)}&username=${encodeURIComponent(username)}&password=${encodeURIComponent(password)}`, {
            method: 'POST'
        });

        if (!createResponse.ok) {
            throw new Error('User creation failed');
        }

        return { success: true };

    } catch (createError) {
        // Handle user creation failure
        showUserCreationFailedMessage();
        return { success: false };
    }
}

// Helper method to show user creation failed message
function showUserCreationFailedMessage() {
    const notification = document.createElement('div');
    notification.innerText = 'User creation failed. The username is taken.';
    notification.className = 'notification error';
    document.body.appendChild(notification);

    setTimeout(() => notification.remove(), 3000);  // Remove after 3 seconds
}

// Helper method to show login failed message after user creation
function showLoginFailedMessage() {
    const notification = document.createElement('div');
    notification.innerText = 'Incorrect username or password.';
    notification.className = 'notification error';
    document.body.appendChild(notification);

    setTimeout(() => notification.remove(), 3000);  // Remove after 3 seconds
}

// Helper method to show user creation success
function showUserCreatedMessage() {
    const notification = document.createElement('div');
    notification.innerText = `Your account was created successfully. You can now login.`;
    notification.className = 'notification success';
    document.body.appendChild(notification);

    setTimeout(() => notification.remove(), 5000);  // Show for 5 seconds
}

// Helper method to show user creation success and display username
function showLoggedInMessage(name) {
    const notification = document.createElement('div');
    notification.innerText = `Welcome, ${name}!`;
    notification.className = 'notification success';
    document.body.appendChild(notification);

    setTimeout(() => notification.remove(), 5000);  // Show for 5 seconds
}

// Helper method to show empty fields notification
function showEmptyFieldsMessage() {
    const notification = document.createElement('div');
    notification.innerText = 'Please fill out all the fields.';
    notification.className = 'notification error';
    document.body.appendChild(notification);

    setTimeout(() => notification.remove(), 3000);  // Remove after 3 seconds
}
function showPasswordMismatchMessage() {
    const notification = document.createElement('div');
    notification.innerText = 'Passwords do not match. Please try again.';
    notification.className = 'notification error';
    document.body.appendChild(notification);

    setTimeout(() => notification.remove(), 3000);  // Remove after 3 seconds
}
