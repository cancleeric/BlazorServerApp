# AuthenticationServer

This project is an ASP.NET Core Web API designed to function as a centralized authentication server. It provides an endpoint for user authentication and issues JSON Web Tokens (JWT) upon successful login.

## Features

- **JWT-based Authentication**: Secures the API using JWTs.
- **Login Endpoint**: Provides a `/api/auth/login` endpoint to authenticate users and issue tokens.
- **Configuration-based JWT Settings**: JWT issuer, audience, and secret key are managed through `appsettings.json`.

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Installation

1. Clone the repository.
2. Navigate to the `AuthenticationServer` directory.
3. Run `dotnet restore` to install the necessary dependencies.

### Configuration

Before running the application, you need to configure the JWT settings in `appsettings.Development.json`:

```json
{
  "Jwt": {
    "Key": "your_super_secret_key_that_is_at_least_32_bytes_long",
    "Issuer": "your_issuer",
    "Audience": "your_audience"
  }
}
```

- **Key**: A secret key for signing the JWT. It must be at least 32 bytes long.
- **Issuer**: The issuer of the JWT.
- **Audience**: The audience of the JWT.

### Running the Application

Run the following command to start the application:

```bash
dotnet run
```

The API will be available at `https://localhost:5001` (or a similar port).

## API Endpoints

### POST /api/auth/login

Authenticates a user and returns a JWT.

**Request Body:**

```json
{
  "username": "admin",
  "password": "password"
}
```

**Success Response (200 OK):**

```json
{
  "token": "your_jwt_token"
}
```

**Error Response (401 Unauthorized):**

Returned if the credentials are invalid.

## Project Structure

- **Controllers/AuthController.cs**: Contains the `Login` endpoint.
- **Models/LoginRequest.cs**: Defines the model for the login request.
- **Models/LoginResponse.cs**: Defines the model for the login response.
- **Program.cs**: Configures the application, including JWT authentication.
- **appsettings.json**: Contains the application settings.
