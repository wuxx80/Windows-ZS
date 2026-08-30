<?php
namespace app\controller\admin;

use app\model\WorkOrder;

class WorkOrderController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $status = input('status');
        $type = input('type');
        $priority = input('priority');
        $customerId = input('customer_id');

        $query = WorkOrder::order('id', 'desc');

        if ($keyword) {
            $query->where('title|description', 'like', '%' . $keyword . '%');
        }
        if ($status !== null && $status !== '') {
            $query->where('status', $status);
        }
        if ($type) {
            $query->where('type', $type);
        }
        if ($priority !== null && $priority !== '') {
            $query->where('priority', $priority);
        }
        if ($customerId) {
            $query->where('customer_id', $customerId);
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'title' => input('title'),
            'description' => input('description'),
            'type' => input('type', 'general'),
            'priority' => input('priority', 0),
            'customer_id' => input('customer_id', 0),
            'assignee_id' => input('assignee_id', 0),
            'expected_at' => input('expected_at'),
            'created_by' => $this->userId,
        ];

        if (empty($data['title'])) {
            return $this->error('param_error', '工单标题不能为空');
        }

        $order = WorkOrder::create($data);
        return $this->success($order, '创建成功');
    }

    public function edit($id)
    {
        $order = WorkOrder::find($id);
        if (!$order) {
            return $this->error('not_found', '工单不存在');
        }

        $data = [];
        foreach (['title', 'description', 'type', 'priority', 'customer_id', 'assignee_id', 'expected_at'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $order->save($data);
        return $this->success($order, '更新成功');
    }

    public function delete($id)
    {
        $order = WorkOrder::find($id);
        if (!$order) {
            return $this->error('not_found', '工单不存在');
        }

        $order->delete();
        return $this->success(null, '删除成功');
    }

    public function detail($id)
    {
        $order = WorkOrder::with(['customer', 'assignee', 'logs'])->find($id);
        if (!$order) {
            return $this->error('not_found', '工单不存在');
        }
        return $this->success($order);
    }

    public function updateStatus($id)
    {
        $order = WorkOrder::find($id);
        if (!$order) {
            return $this->error('not_found', '工单不存在');
        }

        $status = input('status');
        $remark = input('remark');

        if (!$status) {
            return $this->error('param_error', '状态不能为空');
        }

        $order->status = $status;
        $order->remark = $remark;
        $order->save();

        return $this->success($order, '状态更新成功');
    }
}
