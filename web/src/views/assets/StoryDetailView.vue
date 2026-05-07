<script setup lang="ts">
import type { Story, StoryTypeInfo } from '@/api/assets'
import { BookOpen, FolderOpen } from 'lucide-vue-next'
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { getStory, getStoryTypes } from '@/api/assets'
import { ApiError } from '@/api/client'
import ErrorState from '@/components/ErrorState.vue'
import LoadingState from '@/components/LoadingState.vue'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import CardHeader from '@/components/ui/CardHeader.vue'
import CardTitle from '@/components/ui/CardTitle.vue'
import { useAuth } from '@/lib/auth'
import { createStoryTypeLabeler, formatDateTime } from '@/lib/display'
import { recordRecentStory } from '@/lib/recentAssets'

const route = useRoute()
const { state } = useAuth()
const storyTypes = ref<StoryTypeInfo[]>([])
const story = ref<Story | null>(null)
const loading = ref(true)
const error = ref<ApiError | null>(null)
const storyId = computed(() => Number(route.params.storyId))
const storyTypeLabel = computed(() => createStoryTypeLabeler(storyTypes.value))

onMounted(loadPage)
watch(() => route.fullPath, loadPage)

async function loadPage() {
  loading.value = true
  error.value = null

  try {
    const [types, detail] = await Promise.all([
      storyTypes.value.length ? Promise.resolve(storyTypes.value) : getStoryTypes(),
      getStory(storyId.value),
    ])
    storyTypes.value = types
    story.value = detail
    recordRecentStory(state.currentTenant?.id, detail)
  }
  catch (loadError) {
    error.value = loadError instanceof ApiError
      ? loadError
      : new ApiError(0, '剧情详情加载失败，请稍后重试。')
  }
  finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
      <RouterLink class="hover:text-foreground" to="/assets">
        资产目录
      </RouterLink>
      <span>/</span>
      <RouterLink class="hover:text-foreground" to="/stories?page=1&page_size=20">
        剧情
      </RouterLink>
      <template v-if="story?.group">
        <span>/</span>
        <RouterLink class="hover:text-foreground" :to="`/assets/groups/${story.group.id}`">
          {{ story.group.title }}
        </RouterLink>
      </template>
      <span>/</span>
      <span>{{ story?.title || '剧情详情' }}</span>
    </div>

    <LoadingState v-if="loading" label="加载剧情详情" />
    <ErrorState v-else-if="error" :error="error" @retry="loadPage" />

    <template v-else-if="story">
      <Card>
        <CardHeader>
          <div class="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            <BookOpen :size="18" class="text-primary" />
            <span>{{ storyTypeLabel(story.storyType) }}</span>
            <span>·</span>
            <span>{{ story.scenarioId }}</span>
          </div>
          <CardTitle class="text-2xl">
            {{ story.title }}
          </CardTitle>
        </CardHeader>
        <CardContent class="grid gap-4 text-sm md:grid-cols-2 xl:grid-cols-4">
          <div>
            <p class="text-muted-foreground">
              所属剧情集
            </p>
            <RouterLink
              v-if="story.group"
              class="mt-1 inline-flex items-center gap-2 font-medium text-primary hover:underline"
              :to="`/assets/groups/${story.group.id}`"
            >
              <FolderOpen :size="16" />
              {{ story.group.title }}
            </RouterLink>
            <p v-else class="mt-1 font-medium">
              -
            </p>
          </div>
          <div>
            <p class="text-muted-foreground">
              scenario_id
            </p>
            <p class="mt-1 break-all font-medium">
              {{ story.scenarioId }}
            </p>
          </div>
          <div>
            <p class="text-muted-foreground">
              排序
            </p>
            <p class="mt-1 font-medium">
              {{ story.sortOrder }}
            </p>
          </div>
          <div>
            <p class="text-muted-foreground">
              更新时间
            </p>
            <p class="mt-1 font-medium">
              {{ formatDateTime(story.updatedAt) }}
            </p>
          </div>
        </CardContent>
      </Card>

      <section class="rounded-md border bg-card p-5">
        <h2 class="text-base font-semibold">
          阅读入口
        </h2>
        <p class="mt-2 text-sm leading-6 text-muted-foreground">
          原文行、译文版本和译文对照阅读会在下一阶段开放。当前页面用于确认剧情元信息和资产归属。
        </p>
      </section>
    </template>
  </div>
</template>
