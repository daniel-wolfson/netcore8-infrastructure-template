# Husky Git Hooks Setup Script
# Run this to fix and verify your commit-msg hook

Write-Host "🔧 Husky Git Hooks Setup" -ForegroundColor Cyan
Write-Host ""

# Step 1: Configure Git hooks path
Write-Host "1. Configuring Git hooks path..." -ForegroundColor Yellow
git config core.hooksPath .husky
$hooksPath = git config core.hooksPath
Write-Host "   ✅ Git hooks path: $hooksPath" -ForegroundColor Green
Write-Host ""

# Step 2: Initialize Husky
Write-Host "2. Initializing Husky..." -ForegroundColor Yellow
dotnet tool restore
dotnet husky install
Write-Host "   ✅ Husky initialized" -ForegroundColor Green
Write-Host ""

# Step 3: Create commit-msg hook with correct content
Write-Host "3. Creating commit-msg hook..." -ForegroundColor Yellow

$hookContent = @'
#!/bin/sh
. "$(dirname "$0")/_/husky.sh"

# Read commit message
COMMIT_MSG_FILE="$1"
COMMIT_MSG=$(cat "$COMMIT_MSG_FILE")

# Define pattern (must match: type(number): message)
PATTERN="^(feat|fix|refactor|chore)\([0-9]+\): .+$"

# Validate using grep
if ! echo "$COMMIT_MSG" | grep -Eq "$PATTERN"; then
    echo ""
    echo "❌ Invalid commit message format"
    echo ""
    echo "Current message: '$COMMIT_MSG'"
    echo ""
    echo "✅ Required format: type(id): message"
    echo ""
    echo "Types: feat, fix, refactor, chore"
    echo "ID: Task or bug number"
    echo ""
    echo "Examples:"
    echo "  feat(4322): add price calculation logic"
    echo "  fix(1234): resolve null reference exception"
    echo "  refactor(5678): improve error handling"
    echo "  chore(9999): update dependencies"
    echo ""
    exit 1
fi

echo "✅ Commit message format valid"
exit 0
'@

# Write with LF line endings (Unix style)
[System.IO.File]::WriteAllText("$PWD\.husky\commit-msg", $hookContent, [System.Text.UTF8Encoding]::new($false))
Write-Host "   ✅ commit-msg hook created" -ForegroundColor Green
Write-Host ""

# Step 4: Test the hook
Write-Host "4. Testing commit-msg hook..." -ForegroundColor Yellow
Write-Host ""

# Test invalid message
Write-Host "   Test 1: Invalid message 'test' (should fail)" -ForegroundColor Cyan
$testResult = git commit --allow-empty -m "test" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "   ✅ PASS: Invalid message correctly rejected!" -ForegroundColor Green
} else {
    Write-Host "   ❌ FAIL: Invalid message was accepted (hook not working)" -ForegroundColor Red
}
Write-Host ""

# Test valid message
Write-Host "   Test 2: Valid message 'feat(1234): test' (should pass)" -ForegroundColor Cyan
$testResult = git commit --allow-empty -m "feat(1234): test hook validation" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ PASS: Valid message correctly accepted!" -ForegroundColor Green
    # Clean up test commit
    git reset --soft HEAD~1
    Write-Host "   (Test commit removed)" -ForegroundColor Gray
} else {
    Write-Host "   ❌ FAIL: Valid message was rejected" -ForegroundColor Red
}
Write-Host ""

Write-Host "🎉 Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Try committing with:" -ForegroundColor Cyan
Write-Host "  git commit -m 'feat(1234): your message here'" -ForegroundColor White
Write-Host ""
