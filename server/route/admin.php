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

    // Images（特定路由优先于通用集合路由，避免懒匹配误路由到 create）
    Route::post('images/:id/verify', 'admin.Image/verify');
    Route::post('images/:id/convert', 'admin.Image/convert');
    Route::post('images/:id/download', 'admin.Image/download');
    Route::post('images/:id/restore', 'admin.Image/restore');
    Route::post('images/upload', 'admin.Image/upload');
    Route::post('images/uploadComplete', 'admin.Image/uploadComplete');
    Route::post('images/addRemoteUrl', 'admin.Image/addRemoteUrl');
    Route::post('images/batchDelete', 'admin.Image/batchDelete');
    Route::post('images/batchEnable', 'admin.Image/batchEnable');
    Route::post('images/batchDisable', 'admin.Image/batchDisable');
    Route::put('images/:id', 'admin.Image/edit');
    Route::delete('images/:id', 'admin.Image/delete');
    Route::get('images/:id', 'admin.Image/detail');
    Route::get('images', 'admin.Image/index');
    Route::post('images', 'admin.Image/create');

    // Image Sources
    Route::post('imageSources/:id/sync', 'admin.ImageSource/sync');
    Route::put('imageSources/:id', 'admin.ImageSource/edit');
    Route::delete('imageSources/:id', 'admin.ImageSource/delete');
    Route::get('imageSources', 'admin.ImageSource/index');
    Route::post('imageSources', 'admin.ImageSource/create');

    // Image Tags
    Route::put('imageTags/:id', 'admin.ImageTag/edit');
    Route::delete('imageTags/:id', 'admin.ImageTag/delete');
    Route::get('imageTags', 'admin.ImageTag/index');
    Route::post('imageTags', 'admin.ImageTag/create');

    // Clients（自注册 register 为客户端入口，无通用 POST clients）
    Route::post('clients/register', 'admin.Client/register');
    Route::post('clients/:id/approve', 'admin.Client/approve');
    Route::post('clients/:id/block', 'admin.Client/block');
    Route::post('clients/batchApprove', 'admin.Client/batchApprove');
    Route::post('clients/sendCommand', 'admin.Client/sendCommand');
    Route::put('clients/:id', 'admin.Client/edit');
    Route::delete('clients/:id', 'admin.Client/delete');
    Route::get('clients/:id', 'admin.Client/detail');
    Route::get('clients', 'admin.Client/index');

    // Client Groups
    Route::put('clientGroups/:id', 'admin.ClientGroup/edit');
    Route::delete('clientGroups/:id', 'admin.ClientGroup/delete');
    Route::get('clientGroups', 'admin.ClientGroup/index');
    Route::post('clientGroups', 'admin.ClientGroup/create');

    // Client Versions
    Route::post('clientVersions/:id/publish', 'admin.ClientVersion/publish');
    Route::put('clientVersions/:id', 'admin.ClientVersion/edit');
    Route::delete('clientVersions/:id', 'admin.ClientVersion/delete');
    Route::get('clientVersions', 'admin.ClientVersion/index');
    Route::post('clientVersions', 'admin.ClientVersion/create');

    // Tasks（进度上报等带 :id 的特定路由必须优先于通用 tasks）
    Route::get('tasks/:id/logs', 'admin.Task/logs');
    Route::post('tasks/:id/cancel', 'admin.Task/cancel');
    Route::post('tasks/:id/retry', 'admin.Task/retry');
    Route::post('tasks/:id/pause', 'admin.Task/pause');
    Route::post('tasks/:id/resume', 'admin.Task/resume');
    Route::post('tasks/:id/progress', 'admin.Task/progress');
    Route::get('tasks/:id', 'admin.Task/detail');
    Route::get('tasks', 'admin.Task/index');
    Route::post('tasks', 'admin.Task/create');

    // Devices
    Route::get('devices/disks', 'admin.Device/disks');

    // Task Templates
    Route::post('taskTemplates/:id/setDefault', 'admin.TaskTemplate/setDefault');
    Route::put('taskTemplates/:id', 'admin.TaskTemplate/edit');
    Route::delete('taskTemplates/:id', 'admin.TaskTemplate/delete');
    Route::get('taskTemplates', 'admin.TaskTemplate/index');
    Route::post('taskTemplates', 'admin.TaskTemplate/create');

    // Unattend Templates
    Route::get('unattendTemplates/:id/preview', 'admin.Unattend/preview');
    Route::post('unattendTemplates/:id/generateXml', 'admin.Unattend/generateXml');
    Route::post('unattendTemplates/:id/validate', 'admin.Unattend/validate');
    Route::put('unattendTemplates/:id', 'admin.Unattend/edit');
    Route::delete('unattendTemplates/:id', 'admin.Unattend/delete');
    Route::get('unattendTemplates', 'admin.Unattend/index');
    Route::post('unattendTemplates', 'admin.Unattend/create');

    // Software
    Route::post('software/upload', 'admin.Software/upload');
    Route::put('software/:id', 'admin.Software/edit');
    Route::delete('software/:id', 'admin.Software/delete');
    Route::get('software', 'admin.Software/index');
    Route::post('software', 'admin.Software/create');

    // Software Categories
    Route::put('softwareCategories/:id', 'admin.SoftwareCategory/edit');
    Route::delete('softwareCategories/:id', 'admin.SoftwareCategory/delete');
    Route::get('softwareCategories', 'admin.SoftwareCategory/index');
    Route::post('softwareCategories', 'admin.SoftwareCategory/create');

    // Software Templates
    Route::post('softwareTemplates/:id/setDefault', 'admin.SoftwareTemplate/setDefault');
    Route::put('softwareTemplates/:id', 'admin.SoftwareTemplate/edit');
    Route::delete('softwareTemplates/:id', 'admin.SoftwareTemplate/delete');
    Route::get('softwareTemplates', 'admin.SoftwareTemplate/index');
    Route::post('softwareTemplates', 'admin.SoftwareTemplate/create');

    // Drivers
    Route::post('drivers/upload', 'admin.Driver/upload');
    Route::put('drivers/:id', 'admin.Driver/edit');
    Route::delete('drivers/:id', 'admin.Driver/delete');
    Route::get('drivers', 'admin.Driver/index');
    Route::post('drivers', 'admin.Driver/create');

    // Scripts
    Route::post('scripts/:id/execute', 'admin.Script/execute');
    Route::put('scripts/:id', 'admin.Script/edit');
    Route::delete('scripts/:id', 'admin.Script/delete');
    Route::get('scripts', 'admin.Script/index');
    Route::post('scripts', 'admin.Script/create');

    // PE Versions
    Route::put('peVersions/:id', 'admin.PeVersion/edit');
    Route::delete('peVersions/:id', 'admin.PeVersion/delete');
    Route::get('peVersions', 'admin.PeVersion/index');
    Route::post('peVersions', 'admin.PeVersion/create');

    // PE Customize
    Route::post('peCustomize/:id/build', 'admin.PeCustomize/build');
    Route::get('peCustomize/:id/download', 'admin.PeCustomize/download');
    Route::put('peCustomize/:id', 'admin.PeCustomize/edit');
    Route::delete('peCustomize/:id', 'admin.PeCustomize/delete');
    Route::get('peCustomize', 'admin.PeCustomize/index');
    Route::post('peCustomize', 'admin.PeCustomize/create');

    // PXE Configs
    Route::post('pxeConfigs/:id/activate', 'admin.PxeConfig/activate');
    Route::put('pxeConfigs/:id', 'admin.PxeConfig/edit');
    Route::delete('pxeConfigs/:id', 'admin.PxeConfig/delete');
    Route::get('pxeConfigs', 'admin.PxeConfig/index');
    Route::post('pxeConfigs', 'admin.PxeConfig/create');

    // Network Deploy
    Route::post('networkDeploys/:id/start', 'admin.NetworkDeploy/start');
    Route::get('networkDeploys/:id/report', 'admin.NetworkDeploy/report');
    Route::put('networkDeploys/:id', 'admin.NetworkDeploy/edit');
    Route::delete('networkDeploys/:id', 'admin.NetworkDeploy/delete');
    Route::get('networkDeploys', 'admin.NetworkDeploy/index');
    Route::post('networkDeploys', 'admin.NetworkDeploy/create');

    // Users
    Route::get('users/:id', 'admin.User/detail');
    Route::put('users/:id', 'admin.User/edit');
    Route::delete('users/:id', 'admin.User/delete');
    Route::get('users', 'admin.User/index');
    Route::post('users', 'admin.User/create');

    // Customers
    Route::get('customers/:id', 'admin.Customer/detail');
    Route::put('customers/:id', 'admin.Customer/edit');
    Route::delete('customers/:id', 'admin.Customer/delete');
    Route::get('customers', 'admin.Customer/index');
    Route::post('customers', 'admin.Customer/create');

    // Work Orders
    Route::get('workOrders/:id/detail', 'admin.WorkOrder/detail');
    Route::put('workOrders/:id/status', 'admin.WorkOrder/updateStatus');
    Route::put('workOrders/:id', 'admin.WorkOrder/edit');
    Route::delete('workOrders/:id', 'admin.WorkOrder/delete');
    Route::get('workOrders', 'admin.WorkOrder/index');
    Route::post('workOrders', 'admin.WorkOrder/create');

    // Settings（/settings/:key 优先于 /settings，避免懒匹配）
    Route::get('settings/:key', 'admin.Setting/get');
    Route::get('settings', 'admin.Setting/index');
    Route::put('settings', 'admin.Setting/update');

    // Logs
    Route::get('logs/:id', 'admin.Log/detail');
    Route::get('logTypes', 'admin.Log/types');
    Route::post('logs/clear', 'admin.Log/clear');
    Route::get('logs', 'admin.Log/index');

    // Reports
    Route::get('reports/install', 'admin.Report/installReport');
    Route::get('reports/client', 'admin.Report/clientReport');
    Route::get('reports/imageRanking', 'admin.Report/imageRanking');
    Route::get('reports/order', 'admin.Report/orderReport');
    Route::get('reports/workOrder', 'admin.Report/workOrderReport');

    // Notifications
    Route::get('notifications/unread_count', 'admin.Notification/unreadCount');
    Route::post('notifications/:id/read', 'admin.Notification/read');
    Route::post('notifications/batchRead', 'admin.Notification/batchRead');
    Route::get('notifications', 'admin.Notification/index');

    // Scheduled Tasks
    Route::get('scheduledTasks/:id/logs', 'admin.ScheduledTask/logs');
    Route::post('scheduledTasks/:id/trigger', 'admin.ScheduledTask/trigger');
    Route::put('scheduledTasks/:id', 'admin.ScheduledTask/edit');
    Route::delete('scheduledTasks/:id', 'admin.ScheduledTask/delete');
    Route::get('scheduledTasks', 'admin.ScheduledTask/index');
    Route::post('scheduledTasks', 'admin.ScheduledTask/create');

    // Webhook Logs
    Route::post('webhookLogs/:id/retry', 'admin.Webhook/retry');
    Route::get('webhookLogs', 'admin.Webhook/index');

    // Recycle Bin
    Route::post('recycleBin/:id/restore', 'admin.RecycleBin/restore');
    Route::delete('recycleBin/:id', 'admin.RecycleBin/delete');
    Route::delete('recycleBin', 'admin.RecycleBin/clear');
    Route::get('recycleBin', 'admin.RecycleBin/index');
})->middleware(app\middleware\AuthMiddleware::class);
