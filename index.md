---
_layout: landing
---

<link rel="stylesheet" href="styles/landing.css">

<div class="snl-hero">
  <div class="snl-container">
    <div class="snl-hero-content">
      <div class="snl-brand">
        <div>
          <h1 class="snl-title">SteamNetworkLib</h1>
          <div class="snl-tagline">A modern, MelonLoader-first Steamworks.NET networking wrapper</div>
        </div>
      </div>
      <div class="snl-badges">
        <span class="snl-badge">Lobby Management</span>
        <span class="snl-badge">Data Sync</span>
        <span class="snl-badge">P2P Messaging</span>
        <span class="snl-badge">Type-Safe API</span>
      </div>
      <div class="snl-cta">
        <a class="snl-btn snl-btn--primary" href="docs/getting-started.html">Get Started</a>
        <a class="snl-btn snl-btn--secondary" href="docs/introduction.html">Explore Features</a>
        <a class="snl-btn snl-btn--ghost" href="api/SteamNetworkLib.SteamNetworkClient.html">API Reference</a>
      </div>
    </div>
    <div class="snl-hero-code">
      <pre><code class="lang-csharp">// Simple, clean API
var steamNetwork = new SteamNetworkClient();
steamNetwork.Initialize();

// Easy data management
steamNetwork.SetLobbyData("mod_version", "1.0.0");</code></pre>
    </div>
  </div>
</div>

<div class="snl-section">
  <div class="snl-container">
    <h2>What you get</h2>
    <p class="snl-muted">SteamNetworkLib provides a clean, intuitive API that handles the complexity of Steamworks networking for you, made specifically for Schedule 1 mods.</p>
    <div class="snl-feature-grid">
      <div class="snl-card">
        <h3>🔄 Dual Branch Support</h3>
        <p>Seamlessly works with both Mono and IL2CPP, without the complexity of managing Steamworks.NET and Il2CppSteamworks.NET separately.</p>
      </div>
      <div class="snl-card">
        <h3>🏢 Lobby Management</h3>
        <p>Create/join lobbies with async/await, automatic member tracking, and more.</p>
      </div>
      <div class="snl-card">
        <h3>🌐 P2P Communication</h3>
        <p>Type-safe messaging with L2CPP compatibility.</p>
      </div>
      <div class="snl-card">
        <h3>🔧 MelonLoader Optimized</h3>
        <p>Minimal setup, safe error handling, and automatic cleanup designed specifically for mod development.</p>
      </div>
    </div>
  </div>
</div>

<div class="snl-section">
  <div class="snl-container">
    <h2>Next steps</h2>
    <ul>
      <li><a href="docs/introduction.html">Introduction</a> – Features and architecture</li>
      <li><a href="docs/getting-started.html">Getting Started</a> – Set up your first project</li>
      <li><a href="api/SteamNetworkLib.SteamNetworkClient.html">API Reference</a> – Explore the public surface</li>
    </ul>
    <p class="snl-muted">SteamNetworkLib reduces hundreds of lines of complex Steamworks code to clean, maintainable patterns.</p>
  </div>
</div>