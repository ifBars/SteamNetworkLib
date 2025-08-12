# Security Policy

## Supported Versions

The following versions of SteamNetworkLib are currently supported with security updates:

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability in SteamNetworkLib, please follow these steps:

### 🔒 Private Reporting (Recommended)

1. **Use GitHub's Security tab** - Go to the [Security tab](../../security) in this repository
2. **Click "Report a vulnerability"** to create a private security advisory
3. **Provide detailed information** including:
   - A clear description of the vulnerability
   - Steps to reproduce the issue
   - Potential impact assessment
   - Suggested fix (if you have one)

### ✉️ Alternative Contact

If you cannot use GitHub's security reporting feature, you can:
- Create a **private** issue by contacting the maintainers through GitHub
- **Do not create a public issue** for security vulnerabilities

## Response Timeline

- **Acknowledgment**: You'll receive confirmation within **48 hours**
- **Initial Assessment**: We'll provide an initial assessment within **72 hours**
- **Resolution**: We aim to address critical vulnerabilities within **7 days**
- **Updates**: You'll be kept informed throughout the process

## What Happens Next

1. **Acknowledgment** - We'll confirm receipt of your report
2. **Investigation** - Our team will investigate and validate the vulnerability
3. **Fix Development** - We'll develop and test a fix
4. **Disclosure** - Coordinated disclosure after the fix is released
5. **Credit** - You'll be credited in our security advisories (unless you prefer anonymity)

## Responsible Disclosure

We appreciate security researchers who follow responsible disclosure practices:

- ✅ Give us reasonable time to address the issue before public disclosure
- ✅ Provide sufficient detail to reproduce and understand the vulnerability
- ✅ Avoid accessing or modifying other users' data
- ✅ Do not perform destructive testing
- ✅ Test only against your own accounts/data

## Security Scope

SteamNetworkLib handles Steam networking functionality, including:

- 🔐 User authentication and session management
- 🌐 P2P communication between users
- 📊 Lobby and member data synchronization
- 📁 File transfers between users
- 🔄 Real-time data streaming

Please be especially careful when testing these areas and ensure you have proper authorization.

## Out of Scope

The following are generally **not** considered security vulnerabilities:

- Issues in third-party dependencies (report to respective maintainers)
- Social engineering attacks
- Physical attacks
- Denial of Service attacks
- Issues requiring physical access to a user's device

## Bug Bounty

Currently, we do not offer a bug bounty program, but we greatly appreciate responsible disclosure and will acknowledge your contribution.

## Contact

For questions about this security policy, you can:
- Open a public issue for general security questions
- Use the private reporting methods above for actual vulnerabilities

---

**Thank you for helping keep SteamNetworkLib and our users safe!** 🛡️ 