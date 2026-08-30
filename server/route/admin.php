<?php
use think\facade\Route;

// Public routes (no auth required)
Route::get('/', function () {
    return redirect('/index.html');
});
Route::post('api/v1/auth/login', 'admin.Auth/login');

// Auth required routes
Route::group('api/v1', function () {
    // Auth
    Route::post('auth/logout', 'admin.Auth/logout');
    Route::get('auth/profile', 'admin.Auth/profile');
    Route::put('auth/password', 'admin.Auth/updatePassword');

    // Dashboard
    Route::get('dashboard', 'admin.Index/index');

    // Images
    Route::get('images', 'admin.Image/index');
    Route::post('images', 'admin.Image/create');
    Route::put('images/:id', 'admin.Image/edit');
    Route::delete('images/:id', 'admin.Image/delete');
    Route::get('images/:id', 'admin.Image/detail');
    Route::post('images/upload', 'admin.Image/upload');
    Route::post('images/uploadComplete', 'admin.Image/uploadComplete');
    Route::post('images/addRemoteUrl', 'admin.Image/addRemoteUrl');
    Route::post('images/:id/verify', 'admin.Image/verify');
    Route::post('images/:id/convert', 'admin.Image/convert');
    Route::post('images/batchDelete', 'admin.Image/batchDelete');
    Route::post('images/batchEnable', 'admin.Image/batchEnable');
    Route::post('images/batchDisable', 'admin.Image/batchDisable');
    Route::post('images/:id/download', 'admin.Image/download');
    Route::post('images/:id/restore', 'admin.Image/restore');

    // Image Sources
    Route::get('imageSources', 'admin.ImageSource/index');
    Route::post('imageSources', 'admin.ImageSource/create');
    Route::put('imageSources/:id', 'admin.ImageSource/edit');
    Route::delete('imageSources/:id', 'admin.ImageSource/delete');
    Route::post('imageSources/:id/sync', 'admin.ImageSource/sync');

    // Image Tags
    Route::get('imageTags', 'admin.ImageTag/index');
    Route::post('imageTags', 'admin.ImageTag/create');
    Route::put('imageTags/:id', 'admin.ImageTag/edit');
    Route::delete('imageTags/:id', 'admin.ImageTag/delete');

    // Clients
    Route::get('clients', 'admin.Client/index');
    Route::get('clients/:id', 'admin.Client/detail');
    Route::post('clients/:id/approve', 'admin.Client/approve');
    Route::post('clients/batchApprove', 'admin.Client/batchApprove');
    Route::post('clients/:id/block', 'admin.Client/block');
    Route::delete('clients/:id', 'admin.Client/delete');
    Route::put('clients/:id', 'admin.Client/edit');
    Route::post('clients/sendCommand', 'admin.Client/sendCommand');

    // Client Groups
    Route::get('clientGroups', 'admin.ClientGroup/index');
    Route::post('clientGroups', 'admin.ClientGroup/create');
    Route::put('clientGroups/:id', 'admin.ClientGroup/edit');
    Route::delete('clientGroups/:id', 'admin.ClientGroup/delete');

    // Client Versions
    Route::get('clientVersions', 'admin.ClientVersion/index');
    Route::post('clientVersions', 'admin.ClientVersion/create');
    Route::put('clientVersions/:id', 'admin.ClientVersion/edit');
    Route::delete('clientVersions/:id', 'admin.ClientVersion/delete');
    Route::post('clientVersions/:id/publish', 'admin.ClientVersion/publish');

    // Tasks
    Route::get('tasks', 'admin.Task/index');
    Route::get('tasks/:id', 'admin.Task/detail');
    Route::post('tasks', 'admin.Task/create');
    Route::post('tasks/:id/cancel', 'admin.Task/cancel');
    Route::post('tasks/:id/retry', 'admin.Task/retry');
    Route::post('tasks/:id/pause', 'admin.Task/pause');
    Route::post('tasks/:id/resume', 'admin.Task/resume');
    Route::get('tasks/:id/logs', 'admin.Task/logs');

    // Task Templates
    Route::get('taskTemplates', 'admin.TaskTemplate/index');
    Route::post('taskTemplates', 'admin.TaskTemplate/create');
    Route::put('taskTemplates/:id', 'admin.TaskTemplate/edit');
    Route::delete('taskTemplates/:id', 'admin.TaskTemplate/delete');
    Route::post('taskTemplates/:id/setDefault', 'admin.TaskTemplate/setDefault');

    // Unattend Templates
    Route::get('unattendTemplates', 'admin.Unattend/index');
    Route::post('unattendTemplates', 'admin.Unattend/create');
    Route::put('unattendTemplates/:id', 'admin.Unattend/edit');
    Route::delete('unattendTemplates/:id', 'admin.Unattend/delete');
    Route::get('unattendTemplates/:id/preview', 'admin.Unattend/preview');
    Route::post('unattendTemplates/:id/generateXml', 'admin.Unattend/generateXml');
    Route::post('unattendTemplates/:id/validate', 'admin.Unattend/validate');

    // Software
    Route::get('software', 'admin.Software/index');
    Route::post('software', 'admin.Software/create');
    Route::put('software/:id', 'admin.Software/edit');
    Route::delete('software/:id', 'admin.Software/delete');
    Route::post('software/upload', 'admin.Software/upload');

    // Software Categories
    Route::get('softwareCategories', 'admin.SoftwareCategory/index');
    Route::post('softwareCategories', 'admin.SoftwareCategory/create');
    Route::put('softwareCategories/:id', 'admin.SoftwareCategory/edit');
    Route::delete('softwareCategories/:id', 'admin.SoftwareCategory/delete');

    // Software Templates
    Route::get('softwareTemplates', 'admin.SoftwareTemplate/index');
    Route::post('softwareTemplates', 'admin.SoftwareTemplate/create');
    Route::put('softwareTemplates/:id', 'admin.SoftwareTemplate/edit');
    Route::delete('softwareTemplates/:id', 'admin.SoftwareTemplate/delete');
    Route::post('softwareTemplates/:id/setDefault', 'admin.SoftwareTemplate/setDefault');

    // Drivers
    Route::get('drivers', 'admin.Driver/index');
    Route::post('drivers', 'admin.Driver/create');
    Route::put('drivers/:id', 'admin.Driver/edit');
    Route::delete('drivers/:id', 'admin.Driver/delete');
    Route::post('drivers/upload', 'admin.Driver/upload');

    // Scripts
    Route::get('scripts', 'admin.Script/index');
    Route::post('scripts', 'admin.Script/create');
    Route::put('scripts/:id', 'admin.Script/edit');
    Route::delete('scripts/:id', 'admin.Script/delete');
    Route::post('scripts/:id/execute', 'admin.Script/execute');

    // PE Versions
    Route::get('peVersions', 'admin.PeVersion/index');
    Route::post('peVersions', 'admin.PeVersion/create');
    Route::put('peVersions/:id', 'admin.PeVersion/edit');
    Route::delete('peVersions/:id', 'admin.PeVersion/delete');

    // PE Customize
    Route::get('peCustomize', 'admin.PeCustomize/index');
    Route::post('peCustomize', 'admin.PeCustomize/create');
    Route::put('peCustomize/:id', 'admin.PeCustomize/edit');
    Route::delete('peCustomize/:id', 'admin.PeCustomize/delete');
    Route::post('peCustomize/:id/build', 'admin.PeCustomize/build');
    Route::get('peCustomize/:id/download', 'admin.PeCustomize/download');

    // PXE Configs
    Route::get('pxeConfigs', 'admin.PxeConfig/index');
    Route::post('pxeConfigs', 'admin.PxeConfig/create');
    Route::put('pxeConfigs/:id', 'admin.PxeConfig/edit');
    Route::delete('pxeConfigs/:id', 'admin.PxeConfig/delete');
    Route::post('pxeConfigs/:id/activate', 'admin.PxeConfig/activate');

    // Network Deploy
    Route::get('networkDeploys', 'admin.NetworkDeploy/index');
    Route::post('networkDeploys', 'admin.NetworkDeploy/create');
    Route::put('networkDeploys/:id', 'admin.NetworkDeploy/edit');
    Route::delete('networkDeploys/:id', 'admin.NetworkDeploy/delete');
    Route::post('networkDeploys/:id/start', 'admin.NetworkDeploy/start');
    Route::get('networkDeploys/:id/report', 'admin.NetworkDeploy/report');

    // Users
    Route::get('users/:id', 'admin.User/detail');
    Route::get('users', 'admin.User/index');
    Route::post('users', 'admin.User/create');
    Route::put('users/:id', 'admin.User/edit');
    Route::delete('users/:id', 'admin.User/delete');

    // Customers
    Route::get('customers', 'admin.Customer/index');
    Route::post('customers', 'admin.Customer/create');
    Route::put('customers/:id', 'admin.Customer/edit');
    Route::delete('customers/:id', 'admin.Customer/delete');
    Route::get('customers/:id', 'admin.Customer/detail');

    // Work Orders
    Route::get('workOrders', 'admin.WorkOrder/index');
    Route::post('workOrders', 'admin.WorkOrder/create');
    Route::put('workOrders/:id', 'admin.WorkOrder/edit');
    Route::delete('workOrders/:id', 'admin.WorkOrder/delete');
    Route::get('workOrders/:id', 'admin.WorkOrder/detail');
    Route::put('workOrders/:id/status', 'admin.WorkOrder/updateStatus');

    // Settings
    Route::get('settings', 'admin.Setting/index');
    Route::put('settings', 'admin.Setting/update');
    Route::get('settings/:key', 'admin.Setting/get');

    // Logs
    Route::get('logs', 'admin.Log/index');
    Route::get('logs/:id', 'admin.Log/detail');
    Route::get('logTypes', 'admin.Log/types');
    Route::post('logs/clear', 'admin.Log/clear');

    // Reports
    Route::get('reports/install', 'admin.Report/installReport');
    Route::get('reports/client', 'admin.Report/clientReport');
    Route::get('reports/imageRanking', 'admin.Report/imageRanking');
    Route::get('reports/order', 'admin.Report/orderReport');
    Route::get('reports/workOrder', 'admin.Report/workOrderReport');

    // Notifications
    Route::get('notifications', 'admin.Notification/index');
    Route::post('notifications/:id/read', 'admin.Notification/read');
    Route::post('notifications/batchRead', 'admin.Notification/batchRead');
    Route::get('notifications/unreadCount', 'admin.Notification/unreadCount');

    // Scheduled Tasks
    Route::get('scheduledTasks', 'admin.ScheduledTask/index');
    Route::post('scheduledTasks', 'admin.ScheduledTask/create');
    Route::put('scheduledTasks/:id', 'admin.ScheduledTask/edit');
    Route::delete('scheduledTasks/:id', 'admin.ScheduledTask/delete');
    Route::get('scheduledTasks/:id/logs', 'admin.ScheduledTask/logs');
    Route::post('scheduledTasks/:id/trigger', 'admin.ScheduledTask/trigger');

    // Webhook Logs
    Route::get('webhookLogs', 'admin.Webhook/index');
    Route::post('webhookLogs/:id/retry', 'admin.Webhook/retry');

    // Recycle Bin
    Route::get('recycleBin', 'admin.RecycleBin/index');
    Route::post('recycleBin/:id/restore', 'admin.RecycleBin/restore');
    Route::delete('recycleBin/:id', 'admin.RecycleBin/delete');
    Route::delete('recycleBin', 'admin.RecycleBin/clear');
})->middleware(\app\middleware\AuthMiddleware::class);





