-- Add test user with OAuth tokens
INSERT OR REPLACE INTO Users (
    UserId, 
    Email, 
    Name, 
    Provider, 
    CreatedAt, 
    LastLoginAt, 
    IsActive, 
    AccessToken, 
    RefreshToken, 
    TokenExpiresAt
) VALUES (
    'test-user-123',
    'test@example.com',
    'Test User',
    'Google',
    datetime('now'),
    datetime('now'),
    1,
    'ya29.test-access-token-12345',
    '1//test-refresh-token-67890',
    datetime('now', '+1 hour')
);

-- Add email module if it doesn't exist
INSERT OR IGNORE INTO Modules (
    ModuleId,
    Name,
    Description,
    IsEnabled,
    CreatedAt
) VALUES (
    'email',
    'Email',
    'Email management and notifications',
    1,
    datetime('now')
);

-- Add user subscription to email module
INSERT OR REPLACE INTO UserModuleSubscriptions (
    UserId,
    ModuleId,
    IsSubscribed,
    CreatedAt,
    UpdatedAt
) VALUES (
    'test-user-123',
    'email',
    1,
    datetime('now'),
    datetime('now')
);

-- Verify the data
SELECT 'Users with email subscriptions and tokens:' as Info;
SELECT 
    u.Name,
    u.Email,
    u.Provider,
    u.AccessToken IS NOT NULL as HasAccessToken,
    u.TokenExpiresAt,
    COUNT(s.Id) as SubscriptionCount
FROM Users u
LEFT JOIN UserModuleSubscriptions s ON u.UserId = s.UserId AND s.ModuleId = 'email' AND s.IsSubscribed = 1
WHERE u.AccessToken IS NOT NULL AND u.AccessToken != ''
GROUP BY u.Id;