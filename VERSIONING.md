# Semantic Versioning with GitVersion

This project uses [GitVersion](https://gitversion.net/) for automatic semantic versioning based on commit messages and Git history.

## How it Works

- **Automatic Versioning**: Version numbers are calculated automatically based on your commit messages
- **Conventional Commits**: We use conventional commit message format to determine version increments
- **Automatic Tagging**: Successful builds on `main` branch automatically create and push version tags

## Commit Message Format

Use conventional commit message format to control version increments:

### Patch Version Increment (0.4.1 → 0.4.2)
```
fix: fix a bug
chore: update dependencies
docs: update documentation
style: fix formatting
refactor: refactor code structure
perf: improve performance
test: add missing tests
build: update build configuration
ci: update CI pipeline
```

### Minor Version Increment (0.4.1 → 0.5.0)
```
feat: add new feature
feat(api): add new API endpoint
```

### Major Version Increment (0.4.1 → 1.0.0)
```
feat!: breaking change in feature
fix!: breaking fix
BREAKING CHANGE: description of breaking change

# Or in the commit body:
feat: add new API

BREAKING CHANGE: The API response format has changed
```

### Skip Version Increment
```
docs: update README +semver:skip
chore: minor cleanup +semver:none
```

## Branch Strategies

- **main**: Patch increment by default, respects commit message patterns
- **develop**: Minor increment with `-alpha` suffix
- **feature/***: Inherits increment from source branch with `-feature` suffix
- **hotfix/***: Patch increment with `-beta` suffix
- **pull-request**: Inherits increment with `-PullRequest` suffix

## CI/CD Integration

The GitHub Actions workflow:

1. **Versioning Job**: Runs first, calculates version, creates and pushes tags
2. **Build Jobs**: Depend on versioning job, use the calculated version
3. **Docker Images**: Tagged with semantic version, commit SHA, and `latest`

## Examples

```bash
# This commit will bump patch version (0.4.1 → 0.4.2)
git commit -m "fix: resolve authentication issue"

# This commit will bump minor version (0.4.1 → 0.5.0)  
git commit -m "feat: add user profile management"

# This commit will bump major version (0.4.1 → 1.0.0)
git commit -m "feat!: redesign API with breaking changes"

# This commit will not change version
git commit -m "docs: update deployment guide +semver:skip"
```

## Version History

You can see all versions by checking git tags:
```bash
git tag -l
```

## Local Testing

To test version calculation locally:
```bash
dotnet tool install --global GitVersion.Tool
dotnet-gitversion
```

This will show you what version would be calculated for the current commit without making any changes.
