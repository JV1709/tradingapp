# Trading App Client (React)

This project is the React client for the Trading App. The backend must be started first from the C# solution before running the frontend.

## Prerequisites

1. Visual Studio 2022 with these workloads installed:
   - ASP.NET and web development
   - .NET desktop development
2. Node.js and npm installed from Node.js official site (Windows Installer, .msi):
   - https://nodejs.org/

## Run Order

1. Start the backend (C#) first.
2. Start the React client second.

## 1) Run the Backend

1. Open this solution in Visual Studio:
   - ./src/TradingApp/TradingApp.sln
2. Run the solution and start the trading backend service.

Keep the backend running while using the client.

## 2) Run the React Client in the Terminal

From this folder:

- ./src/TradingClientReact

Run:

```bash
npm run dev
```

After startup, copy the localhost URL shown in the terminal (for example, http://localhost:5173) and open it in your browser.

## Build (Optional)

To create a production build:

```bash
npm run build
```

## Known Limitations

- The app does not support more than 4 tabs in the same browser.
- The app works across different browsers.
