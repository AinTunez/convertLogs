# convertLogs

This application has the same functionality as Blackboard's [convertLogs.py](https://help.blackboard.com/Learn/Administrator/SaaS/System_Management/Logs#format-logs-to-be-more-readable_OTP-3) script, but also supports drag/drop of folders or ZIP files onto the application for quick extraction. Download a ZIP package from Saas, drag onto the application, and it does the rest for you.

1. Log on to your Blackboard instance as System Administrator and go to System Admin --> Manage Content --> Internal --> Logs.
2. Navigate to the year/month/day for which you want to view the logs. You can technically extract multiple days at once, but the process could take a while.
3. Check the boxes next to the hours you want and click Download Package. Save the file to your preferred location.
4. Drag the saved ZIP file onto the `convertLogs` executable.

The extraction process should then start automatically and yield the same results as the BB script. You can also extract the package manually and drag the folder onto the executable.
