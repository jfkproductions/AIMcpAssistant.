const sqlite3 = require('sqlite3').verbose();

// Open database
const db = new sqlite3.Database('./backend/AIMcpAssistant.Api/aimcp.db', (err) => {
  if (err) {
    console.error('Error opening database:', err.message);
    return;
  }
  console.log('Connected to SQLite database.');
});

// Query modules
db.all('SELECT * FROM Modules', [], (err, rows) => {
  if (err) {
    console.error('Error querying Modules:', err.message);
    return;
  }
  console.log('\n=== MODULES TABLE ===');
  console.log(JSON.stringify(rows, null, 2));
});

// Query user subscriptions
db.all('SELECT * FROM UserModuleSubscriptions', [], (err, rows) => {
  if (err) {
    console.error('Error querying UserModuleSubscriptions:', err.message);
    return;
  }
  console.log('\n=== USER MODULE SUBSCRIPTIONS TABLE ===');
  console.log(JSON.stringify(rows, null, 2));
  
  // Close database
  db.close((err) => {
    if (err) {
      console.error('Error closing database:', err.message);
    } else {
      console.log('Database connection closed.');
    }
  });
});