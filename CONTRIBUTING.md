# Contributing to SteamNetworkLib

Thank you for your interest in contributing to SteamNetworkLib! This document provides guidelines for contributing to the project.

## Code of Conduct

This project is committed to providing a welcoming and inclusive environment for all contributors. Please be respectful and constructive in all interactions.

## How to Contribute

### Reporting Issues

1. Check if the issue has already been reported
2. Use the issue template and provide:
   - Clear description of the problem
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, .NET version, etc.)

### Suggesting Features

1. Check if the feature has already been suggested
2. Provide a clear description of the feature
3. Explain the use case and benefits
4. Consider implementation complexity

### Submitting Code

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Add tests if applicable
5. Ensure code follows the project's style guidelines
6. Commit your changes (`git commit -m 'Add amazing feature'`)
7. Push to the branch (`git push origin feature/amazing-feature`)
8. Open a Pull Request

## Development Setup

1. Clone the repository
2. Copy `Directory.Build.user.props.template` to `Directory.Build.user.props`
3. Configure your game installation paths in `Directory.Build.user.props`
4. Build the project: `dotnet build -c Mono` or `dotnet build -c Il2cpp`

## Code Style Guidelines

- Use C# coding conventions
- Follow the existing code style in the project
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Keep methods focused and reasonably sized

## Testing

- Test your changes thoroughly
- Ensure compatibility with both Mono and IL2CPP runtimes
- Test with different Unity versions if applicable
- Verify Steam networking functionality

## License

By contributing to SteamNetworkLib, you agree that your contributions will be licensed under the MIT License.

## Attribution

When contributing, please ensure that:

1. You have the right to contribute the code
2. Any third-party code is properly attributed
3. License requirements are met for any new dependencies

## Questions?

If you have questions about contributing, please open an issue or contact the maintainers. 