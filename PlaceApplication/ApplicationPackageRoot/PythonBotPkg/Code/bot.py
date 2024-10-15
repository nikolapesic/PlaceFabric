import requests
import random
import string
import time
import sys
import signal
import os

# Global flag to indicate when the bot should stop
shutdown_flag = False

def handle_shutdown(signum, frame):
    global shutdown_flag
    print(f"Received shutdown signal: {signum}")
    shutdown_flag = True

def create_user(base_url, firstName, lastName, username, password):
    params = {'firstName' : firstName, 'lastName' : lastName, 'username': username, 'password': password}
    response = requests.post(f"{base_url}/createUser", params=params)  # Using query parameters
    return response.status_code == 200

def login(base_url, username, password):
    params = {'username': username, 'password': password}
    while True:  # Retry until login is successful
        response = requests.post(f"{base_url}/login", params=params)  # Using query parameters
        if response.status_code == 200:
            return response.json()  # Return the token on success
        else:
            print(f"Login failed, retrying in 5 seconds...")
            time.sleep(5)  # Wait 5 seconds before retrying

def get_remaining_cooldown(base_url, username, token):
    params = {'username': username, 'token': token}
    response = requests.get(f"{base_url}/remainingCooldown", params=params)  # Using query parameters
    if response.status_code == 401:  # Unauthorized, session expired
        return 'unauthorized'
    if response.status_code == 200:
        return response.json()  # Cooldown time in seconds
    return None

def place_pixel(base_url, username, token, x, y, color):
    params = {
        'username': username,
        'token': token,
        'x': x,
        'y': y,
        'color': color
    }
    response = requests.post(f"{base_url}/place", params=params)  # Using query parameters
    return response.status_code

def generate_random_color():
    return f'#{random.randint(0, 0xFFFFFF):06x}'  # Generates a random hex color

def generate_random_string(length):
    return ''.join(random.choices(string.ascii_letters + string.digits, k=length))

def get_grid_dimensions(base_url):
    # Fetch grid data from /getGrid endpoint and extract max height and width
    response = requests.get(f"{base_url}/getGrid")
    if response.status_code == 200:
        grid_data = response.json()
        max_x = 0
        max_y = 0
        for key in grid_data['colors']:
            x, y = map(int, key.split(','))
            max_x = max(max_x, x)
            max_y = max(max_y, y)
        if max_x == 0:
            raise Exception("Failed to get grid dimensions")
        return max_x + 1, max_y + 1  # Assuming zero-indexed grid
    else:
        raise Exception("Failed to get grid dimensions")

# Directions for moving in the grid
DIRECTIONS = [(0, 1), (0, -1), (1, 0), (-1, 0)]  # Right, Left, Down, Up

def bot_loop(cluster_ip):
    global shutdown_flag
    base_url = f"http://{cluster_ip}:8124"
    grid_width, grid_height = get_grid_dimensions(base_url)  # Dynamically get grid size
    
    # Generate random username and password
    username = generate_random_string(8)  # Random 8-character username
    password = generate_random_string(12)  # Random 12-character password
    firstName = generate_random_string(5)  # Random 5-character first name
    lastName = generate_random_string(5)  # Random 5-character last name

    if create_user(base_url, firstName, lastName, username, password):
        token = login(base_url, username, password)  # Retry mechanism is inside the login
        if not token:
            return  # Abort if login fails

        # Initialize snake position
        x, y = random.randint(0, grid_width - 1), random.randint(0, grid_height - 1)

        # Choose a fixed random color at the start
        color = generate_random_color()

        # Main loop
        while token and not shutdown_flag:
            # Get remaining cooldown
            cooldown = get_remaining_cooldown(base_url, username, token)
            if cooldown == 'unauthorized':
                token = login(base_url, username, password)  # Re-login if unauthorized
                continue

            # Wait for cooldown before placing a new pixel
            if cooldown is not None:
                time.sleep(cooldown)

            # Pick a new random target vertex
            target_x = random.randint(0, grid_width - 1)
            target_y = random.randint(0, grid_height - 1)

            # Move towards the target
            while (x != target_x or y != target_y) and not shutdown_flag:
                # Choose a direction to move closer to the target
                possible_moves = []
                if x < target_x:
                    possible_moves.append((1, 0))  # Move right
                elif x > target_x:
                    possible_moves.append((-1, 0))  # Move left
                if y < target_y:
                    possible_moves.append((0, 1))  # Move down
                elif y > target_y:
                    possible_moves.append((0, -1))  # Move up

                # Randomly pick one of the possible moves
                if possible_moves:
                    move = random.choice(possible_moves)
                    new_x = x + move[0]
                    new_y = y + move[1]

                    # Place the pixel at the new position
                    result = place_pixel(base_url, username, token, new_x, new_y, color)

                    if result == 401:  # Unauthorized
                        token = login(base_url, username, password)  # Re-login
                    elif result == 200:  # Pixel placed successfully
                        # Update current position
                        x, y = new_x, new_y
                    elif result == 400:  # Bad request, cooldown not expired
                        time.sleep(1)  # Retry after a short wait
                else:
                    break  # No more valid moves, stop

    else:
        print("Failed to create user")

if __name__ == "__main__":
    # Get the cluster IP from the environment variable
    cluster_ip = os.getenv('EndpointUrl')
    
    if not cluster_ip:
        print("EndpointUrl environment variable not set.")
        sys.exit(1)

    signal.signal(signal.SIGTERM, handle_shutdown)  # Catch termination signal
    signal.signal(signal.SIGINT, handle_shutdown)   # Catch interrupt signal

    while not shutdown_flag:
        try:
            bot_loop(cluster_ip)
        except Exception as e:
            print(f"An error occurred: {e}")
            print("Restarting in 5 seconds...")
            time.sleep(5)  # Wait for 5 seconds before retrying

    print("Exited")
